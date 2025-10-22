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
		private string _msg;
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
		public static bool Readable(SafeNativeHandle streamHandle)
		{
			byte[] aPeekBuffer = new byte[1];
			uint aPeekedBytes = 0;
			uint aAvailBytes = 0;
			uint aLeftBytes = 0;

			bool aPeekedSuccess = Kernel32.PeekNamedPipe(
				streamHandle,
				aPeekBuffer,
				1,
				ref aPeekedBytes, 
				ref aAvailBytes, 
				ref aLeftBytes);

			if (aPeekedSuccess && aPeekBuffer[0] != 0)
				return true;
			else
				return false;
		}

		public static SafeNativeHandle DuplicateTokenAsPrimary(SafeHandle hToken)
		{
			SafeNativeHandle pDupToken;
			if (!DuplicateTokenEx(hToken, 0, IntPtr.Zero, InteropTypes.SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, InteropTypes.TOKEN_TYPE.TokenPrimary, out pDupToken))
			{
				throw new Win32Exception("Failed to duplicate impersonation token as primary");
			}

			return pDupToken;
		}

		//public static SafeNativeHandle GetSessionUserToken(uint sessionId, bool elevated = false)
		//{
		//	SafeNativeHandle hSessionUserToken;

		//	if (!Wtsapi32.WTSQueryUserToken(sessionId, out hSessionUserToken))
		//		throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to query user token for session {sessionId}");

		//	return hSessionUserToken!;
		//}

		public static SafeNativeHandle GetSessionUserToken(bool elevated = false)
		{
			var activeSessionId = INVALID_SESSION_ID;
			//var pSessionInfo = IntPtr.Zero;
			var sessionCount = 0;

			if (Wtsapi32.WTSEnumerateSessions(WTS_CURRENT_SERVER_HANDLE, 0, 1, out var pSessionInfo, out sessionCount))
			{
				var elemSz = Marshal.SizeOf<WTS_SESSION_INFO>();
				var current = pSessionInfo.DangerousGetHandle();

				for (var i = 0; i < sessionCount; i++)
				{
					var si = Marshal.PtrToStructure<WTS_SESSION_INFO>(current);

					current = IntPtr.Add(current, elemSz);

					if (si.State == WTS_CONNECTSTATE_CLASS.Active)
					{
						activeSessionId = si.SessionId;
						break;
					}
				}
			}

			pSessionInfo.Dispose();

			if (activeSessionId == INVALID_SESSION_ID)
			{
				Console.WriteLine("Invalid session ID, getting active...");
				activeSessionId = Wtsapi32.WTSGetActiveConsoleSessionId();
			}

			if (!WTSQueryUserToken(activeSessionId, out SafeNativeHandle hImpersonationToken)) 
			{
				throw new Win32Exception("WTSQueryUserToken failed to get access token");
			}

			using (hImpersonationToken)
			{
				// First see if the token is the full token or not. If it is a limited token we need to get the
				// linked (full/elevated token) and use that for the CreateProcess task. If it is already the full or
				// default token then we already have the best token possible.
				InteropTypes.TokenElevationType elevationType = GetTokenElevationType(hImpersonationToken);
				if (elevationType == InteropTypes.TokenElevationType.TokenElevationTypeLimited && elevated == true)
				{
					using (var linkedToken = GetTokenLinkedToken(hImpersonationToken))
						return DuplicateTokenAsPrimary(linkedToken);
				}
				else
				{
					return DuplicateTokenAsPrimary(hImpersonationToken);
				}
			}
		}

		private static InteropTypes.TokenElevationType GetTokenElevationType(SafeHandle hToken)
		{
			using (SafeHGlobalBuffer tokenInfo = GetTokenInformation(hToken, 18))
			{
				return (InteropTypes.TokenElevationType)Marshal.ReadInt32(tokenInfo.DangerousGetHandle());
			}
		}
		private static SafeNativeHandle GetTokenLinkedToken(SafeHandle hToken)
		{
			using (SafeHGlobalBuffer tokenInfo = GetTokenInformation(hToken, 19))
			{
				return new SafeNativeHandle(Marshal.ReadIntPtr(tokenInfo.DangerousGetHandle()));
			}
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
