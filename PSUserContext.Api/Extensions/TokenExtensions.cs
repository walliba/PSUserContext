using PSUserContext.Api.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using PSUserContext.Api.Interop;
using static PSUserContext.Api.Interop.InteropTypes;

namespace PSUserContext.Api.Extensions
{
	public static class TokenExtensions
	{
		private static TokenElevationType GetTokenElevationType(SafeHandle hToken)
		{
			using SafeHGlobalBuffer tokenInfo = GetTokenInformation(hToken, 18);
			return (TokenElevationType)Marshal.ReadInt32(tokenInfo.DangerousGetHandle());
		}
		private static SafeNativeHandle GetTokenLinkedToken(SafeHandle hToken)
		{
			using (SafeHGlobalBuffer tokenInfo = GetTokenInformation(hToken, 19))
				return new SafeNativeHandle(Marshal.ReadIntPtr(tokenInfo.DangerousGetHandle()));
		}

		private static SafeHGlobalBuffer GetTokenInformation(SafeHandle hToken, uint infoClass)
		{
			if (hToken is null || hToken.IsInvalid)
				throw new ArgumentException("Invalid token handle.", nameof(hToken));

			// Constants
			const int ERROR_INSUFFICIENT_BUFFER = 122;
			const int ERROR_BAD_LENGTH = 24;

			// First call — query required buffer size
			if (!Advapi32.GetTokenInformation(
					hToken,
					infoClass,
					SafeHGlobalBuffer.Null,  // see helper below
					0,
					out uint requiredLength))
			{
				int error = Marshal.GetLastWin32Error();
				if (error != ERROR_INSUFFICIENT_BUFFER && error != ERROR_BAD_LENGTH)
					throw new Interop.Win32Exception(error, $"GetTokenInformation({infoClass}) failed to query buffer size.");
			}

			// Kinda silly since its a uint, but oh well
			if (requiredLength <= 0)
				throw new InvalidOperationException($"GetTokenInformation({infoClass}) returned zero-length buffer requirement.");

			// Allocate managed-safe memory for the result
			SafeHGlobalBuffer buffer = new SafeHGlobalBuffer(requiredLength);

			try
			{
				// Second call — retrieve actual information
				if (!Advapi32.GetTokenInformation(hToken, infoClass, buffer, requiredLength, out _))
					throw new Interop.Win32Exception(Marshal.GetLastWin32Error(), $"GetTokenInformation({infoClass}) failed.");

				return buffer; // ownership transferred to caller
			}
			catch
			{
				buffer.Dispose(); // prevent leaks if second call fails
				throw;
			}
		}

		public static SafeNativeHandle DuplicateTokenAsPrimary(SafeHandle hToken)
		{
			if (!Advapi32.DuplicateTokenEx(hToken, 0, IntPtr.Zero, SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, InteropTypes.TOKEN_TYPE.TokenPrimary, out SafeNativeHandle pDupToken))
				throw new Interop.Win32Exception("Failed to duplicate impersonation token as primary");

			return pDupToken;
		}

		public static Dictionary<String, PrivilegeAttributes> GetTokenPrivileges()
		{
			Dictionary<string, PrivilegeAttributes> privileges = new Dictionary<string, PrivilegeAttributes>();

			if (!Advapi32.OpenProcessToken(Kernel32.GetCurrentProcess(), TokenAccessLevels.Query, out var hToken))
				throw new Interop.Win32Exception(Marshal.GetLastWin32Error(), "Failed to get current process token");

			using (hToken)
			using (SafeHGlobalBuffer tokenInfo = GetTokenInformation(hToken, 3))
			{
				IntPtr basePtr = tokenInfo.DangerousGetHandle();

				// Read the privilege count manually (first 4 bytes)
				int privilegeCount = Marshal.ReadInt32(basePtr);
				IntPtr luidPtr = IntPtr.Add(basePtr, sizeof(uint));

				for (int i = 0; i < privilegeCount; i++)
				{
					var info = Marshal.PtrToStructure<InteropTypes.LUID_AND_ATTRIBUTES>(luidPtr);

					// Look up privilege name
					uint nameLen = 0;
					Advapi32.LookupPrivilegeName(null, ref info.Luid, null, ref nameLen);

					StringBuilder name = new StringBuilder((int)(nameLen + 1));
					if (!Advapi32.LookupPrivilegeName(null, ref info.Luid, name, ref nameLen))
						throw new Interop.Win32Exception(Marshal.GetLastWin32Error(), "LookupPrivilegeName failed");

					privileges[name.ToString()] = info.Attributes;

					// Advance pointer
					luidPtr = IntPtr.Add(luidPtr, Marshal.SizeOf(typeof(InteropTypes.LUID_AND_ATTRIBUTES)));
				}
			}

			return privileges;
		}

		public static SafeNativeHandle GetSessionUserToken(string username, bool elevated = false)
		{
			var sessions = SessionExtensions.GetSessions();

			if (sessions.Count < 1)
				throw new InvalidOperationException("No active sessions found.");

			// Find the active session that matches the given username and pass its session id to the other overload
			var match = sessions.FirstOrDefault(s =>
				s.UserName.Equals(username, StringComparison.OrdinalIgnoreCase)
			);

			if (match is null)
				throw new InvalidOperationException($"No sessions found matching user '{username}'");

			return GetSessionUserToken(match.Id, elevated);
		}

		public static SafeNativeHandle GetSessionUserToken(uint sessionId, bool elevated = false)
		{
			if (sessionId == INVALID_SESSION_ID)
				sessionId = SessionExtensions.GetActiveConsoleSessionId()
					?? throw new InvalidOperationException("No active console session found. This typically occurs when no user is logged in.");

			if (!Wtsapi32.WTSQueryUserToken(sessionId, out SafeNativeHandle hSessionUserToken))
			{
				int error = Marshal.GetLastWin32Error();

				if (error is 2 or 87 or 7022)
					throw new InvalidOperationException($"The session ID {sessionId} does not exist");

				throw new Interop.Win32Exception(Marshal.GetLastWin32Error(), $"Failed to query user token for session {sessionId}");
			}


			// todo: investigate if this using causes issues with the returned handle / make DuplicateTokenAsPrimary handle disposal
			using (hSessionUserToken)
			{
				// First see if the token is the full token or not. If it is a limited token we need to get the
				// linked (full/elevated token) and use that for the CreateProcess task. If it is already the full or
				// default token then we already have the best token possible.
				TokenElevationType elevationType = GetTokenElevationType(hSessionUserToken);
				if (elevationType == TokenElevationType.TokenElevationTypeLimited && elevated == true)
				{
					using var linkedToken = GetTokenLinkedToken(hSessionUserToken);
					return DuplicateTokenAsPrimary(linkedToken);
				}
				else
				{
					return DuplicateTokenAsPrimary(hSessionUserToken);
				}
			}
		}
	}
}
