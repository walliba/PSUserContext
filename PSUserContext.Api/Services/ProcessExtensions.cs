using Microsoft.Win32.SafeHandles;
using PSUserContext.Api.Helpers;
using PSUserContext.Api.Native;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using static PSUserContext.Api.Native.Advapi32;
using static PSUserContext.Api.Native.Wtsapi32;

namespace PSUserContext.Api.Services
{
	public class Win32Exception : System.ComponentModel.Win32Exception
	{
		private readonly string _msg;
		public Win32Exception(string message) : this(Marshal.GetLastWin32Error(), message) { }
		public Win32Exception(int errorCode, string message) : base(errorCode)
		{
			_msg = String.Format("{0} ({1}, Win32ErrorCode {2} - 0x{2:X8})", message, base.Message, errorCode);
		}
		public override string Message { get { return _msg; } }
		public static explicit operator Win32Exception(string message) { return new Win32Exception(message); }
	}

	public static class ProcessExtensions
	{
		public static SafeNativeHandle DuplicateTokenAsPrimary(SafeHandle hToken)
		{
			if (!DuplicateTokenEx(hToken, 0, IntPtr.Zero, InteropTypes.SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, InteropTypes.TOKEN_TYPE.TokenPrimary, out SafeNativeHandle pDupToken))
				throw new Win32Exception("Failed to duplicate impersonation token as primary");

			return pDupToken;
		}

		public static SafeNativeHandle GetSessionUserToken(uint sessionId, bool elevated = false)
		{
			if (sessionId == INVALID_SESSION_ID)
				sessionId = WTSExtensions.GetActiveConsoleSessionId()
					?? throw new InvalidOperationException("No active console session found. This typically occurs when no user is logged in.");

			if (!WTSQueryUserToken(sessionId, out SafeNativeHandle hSessionUserToken))
			{
				int error = Marshal.GetLastWin32Error();

				if (error == 0x2)
					throw new InvalidOperationException($"The session ID {sessionId} does not exist");

				throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to query user token for session {sessionId}");
			}

			using (hSessionUserToken)
			{
				// First see if the token is the full token or not. If it is a limited token we need to get the
				// linked (full/elevated token) and use that for the CreateProcess task. If it is already the full or
				// default token then we already have the best token possible.
				InteropTypes.TokenElevationType elevationType = GetTokenElevationType(hSessionUserToken);
				if (elevationType == InteropTypes.TokenElevationType.TokenElevationTypeLimited && elevated == true)
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

		private static InteropTypes.TokenElevationType GetTokenElevationType(SafeHandle hToken)
		{
			using SafeHGlobalBuffer tokenInfo = GetTokenInformation(hToken, 18);
			return (InteropTypes.TokenElevationType)Marshal.ReadInt32(tokenInfo.DangerousGetHandle());
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
					throw new Win32Exception(error, $"GetTokenInformation({infoClass}) failed to query buffer size.");
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
					throw new Win32Exception(Marshal.GetLastWin32Error(), $"GetTokenInformation({infoClass}) failed.");

				return buffer; // ownership transferred to caller
			}
			catch
			{
				buffer.Dispose(); // prevent leaks if second call fails
				throw;
			}
		}

		public static Dictionary<String, InteropTypes.PrivilegeAttributes> GetTokenPrivileges()
		{
			Dictionary<string, InteropTypes.PrivilegeAttributes> privileges = new Dictionary<string, InteropTypes.PrivilegeAttributes>();

			if (!Advapi32.OpenProcessToken(Kernel32.GetCurrentProcess(), TokenAccessLevels.Query, out var hToken))
				throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to get current process token");

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
						throw new Win32Exception(Marshal.GetLastWin32Error(), "LookupPrivilegeName failed");

					privileges[name.ToString()] = info.Attributes;

					// Advance pointer
					luidPtr = IntPtr.Add(luidPtr, Marshal.SizeOf(typeof(InteropTypes.LUID_AND_ATTRIBUTES)));
				}
			}

			return privileges;
		}
	}
}
