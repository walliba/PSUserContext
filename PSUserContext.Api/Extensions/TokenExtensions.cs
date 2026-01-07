// using PSUserContext.Api.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
// using PSUserContext.Api.Interop;
// using static PSUserContext.Api.Interop.InteropTypes;

namespace PSUserContext.Api.Extensions
{
	public static class TokenExtensions
	{
		private const uint INVALID_SESSION_ID = 0xFFFFFFFF;
		// private static TOKEN_ELEVATION_TYPE GetTokenElevationType(SafeHandle hToken)
		// {
		// 	using var tokenInfo = GetTokenInformation(hToken, TOKEN_INFORMATION_CLASS.TokenElevationType);
		// 	return (TOKEN_ELEVATION_TYPE)Marshal.ReadInt32(tokenInfo.DangerousGetHandle());
		// }
		
		// private static SafeAccessTokenHandle GetTokenLinkedToken(SafeHandle hToken)
		// {
		// 	using var tokenInfo = GetTokenInformation(hToken, TOKEN_INFORMATION_CLASS.TokenLinkedToken);
		// 	return new SafeAccessTokenHandle(Marshal.ReadIntPtr(tokenInfo.DangerousGetHandle()));
		// }
		private static T GetTokenInfoStruct<T>(SafeHandle token, TOKEN_INFORMATION_CLASS tokenInformationClass)
			where T : unmanaged
		{
			var buffer = GetTokenInformation(token, tokenInformationClass);
			
			if (buffer.Length < Unsafe.SizeOf<T>())
				throw new InvalidOperationException($"Unexpected size for {typeof(T).Name}");

			return MemoryMarshal.Read<T>(buffer);
		}
		
		private static TOKEN_ELEVATION_TYPE GetTokenElevationType(SafeHandle token)
			=> GetTokenInfoStruct<TOKEN_ELEVATION_TYPE>(token, TOKEN_INFORMATION_CLASS.TokenElevationType);

		private static SafeHandle GetTokenLinkedToken(SafeHandle token)
		{
			var linkedToken = GetTokenInfoStruct<TOKEN_LINKED_TOKEN>(token, TOKEN_INFORMATION_CLASS.TokenLinkedToken);
			
			return new SafeAccessTokenHandle(linkedToken.LinkedToken);
		}
		
		private static TOKEN_PRIVILEGES GetTokenPrivileges(SafeHandle token)
			=> GetTokenInfoStruct<TOKEN_PRIVILEGES>(token, TOKEN_INFORMATION_CLASS.TokenPrivileges);
		
		private static Span<byte> GetTokenInformation(SafeHandle hToken, TOKEN_INFORMATION_CLASS infoClass)
		{
			if (hToken is null || hToken.IsInvalid)
				throw new ArgumentException("Invalid token handle.", nameof(hToken));

			// Constants
			const int ERROR_INSUFFICIENT_BUFFER = 122;
			const int ERROR_BAD_LENGTH = 24;
			
			if (!PInvoke.GetTokenInformation(hToken, infoClass, Span<byte>.Empty, out uint needLength))
			{
				int error = Marshal.GetLastWin32Error();
				if (error != ERROR_INSUFFICIENT_BUFFER && error != ERROR_BAD_LENGTH)
					throw new Interop.Win32Exception(error, $"GetTokenInformation({infoClass}) failed to query buffer size.");
			}
			
			if (needLength == 0)
				throw new InvalidOperationException($"TokenInformation returned zero needed size for {infoClass}");


			Span<byte> buffer = new Span<byte>(new byte[needLength]);
			
			if (!PInvoke.GetTokenInformation(hToken, infoClass, buffer, out _))
				throw new Interop.Win32Exception(Marshal.GetLastWin32Error(), $"GetTokenInformation({infoClass}) failed.");
			
			return buffer;
		}

		private static SafeFileHandle DuplicateTokenAsPrimary(SafeHandle hToken)
		{
			// todo: should I check token privileges here?
			
			if (!Windows.Win32.PInvoke.DuplicateTokenEx(hToken, 0, null, Windows.Win32.Security.SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, Windows.Win32.Security.TOKEN_TYPE.TokenPrimary, out var pDupToken))
				throw new Interop.Win32Exception("Failed to duplicate impersonation token as primary");

			return pDupToken;
		}

		private static Dictionary<String, TOKEN_PRIVILEGES_ATTRIBUTES> GetTokenPrivileges()
		{
			Dictionary<string, TOKEN_PRIVILEGES_ATTRIBUTES> privileges = new Dictionary<string, TOKEN_PRIVILEGES_ATTRIBUTES>();

			// if (!PInvoke.OpenProcessToken(Kernel32.GetCurrentProcess(), TokenAccessLevels.Query, out var hToken))
			// 	throw new Interop.Win32Exception(Marshal.GetLastWin32Error(), "Failed to get current process token");
			if (!PInvoke.OpenProcessToken(PInvoke.GetCurrentProcess_SafeHandle(), TOKEN_ACCESS_MASK.TOKEN_QUERY,
				    out var hProcessToken))
			{
				throw new InvalidOperationException("Failed to open process token.");
			}

			using (hProcessToken)
			{
				var tokenPrivileges = GetTokenPrivileges(hProcessToken);

				for (int i = 0; i < tokenPrivileges.PrivilegeCount; i++)
				{
					var info = tokenPrivileges.Privileges[i];
					
					uint needed = 0;
					
					PInvoke.LookupPrivilegeName(null, info.Luid, Span<char>.Empty, ref needed);
					Span<char> buffer = new char[needed];
					uint size = (uint)buffer.Length;
					PInvoke.LookupPrivilegeName(null, info.Luid, buffer, ref size);
					
					privileges[buffer.ToString()] = info.Attributes;
				}
				// IntPtr basePtr = tokenInfo.DangerousGetHandle();
				
				// Read the privilege count manually (first 4 bytes)
				// int privilegeCount = Marshal.ReadInt32(basePtr);
				// IntPtr luidPtr = IntPtr.Add(basePtr, sizeof(uint));
				//
				// for (int i = 0; i < privilegeCount; i++)
				// {
				// 	var info = Marshal.PtrToStructure<LUID_AND_ATTRIBUTES>(luidPtr);
				//
				// 	// Look up privilege name
				// 	uint nameLen = 0;
				// 	// Advapi32.LookupPrivilegeName(null, ref info.Luid, null, ref nameLen);
				// 	PInvoke.LookupPrivilegeName(null, info.Luid,);
				//
				// 	StringBuilder name = new StringBuilder((int)(nameLen + 1));
				// 	if (!Advapi32.LookupPrivilegeName(null, ref info.Luid, name, ref nameLen))
				// 		throw new Interop.Win32Exception(Marshal.GetLastWin32Error(), "LookupPrivilegeName failed");
				// 	
				// 	privileges[name.ToString()] = info.Attributes;
				//
				// 	// Advance pointer
				// 	luidPtr = IntPtr.Add(luidPtr, Marshal.SizeOf(typeof(InteropTypes.LUID_AND_ATTRIBUTES)));
				// }
			}

			return privileges;
		}

		public static bool HasTokenPrivilege(string privilege)
		{
			var privileges = GetTokenPrivileges();
			
			if (!privileges.TryGetValue(privilege, out var attributes))
				return false;

			return attributes != 0;
		}

		public static SafeFileHandle GetSessionUserToken(string username, bool elevated = false)
		{
			var sessions = SessionExtensions.GetSessions().ToList();

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

		public static unsafe SafeFileHandle GetSessionUserToken(uint sessionId, bool elevated = false)
		{
			if (sessionId == INVALID_SESSION_ID)
				sessionId = SessionExtensions.GetActiveConsoleSessionId()
					?? throw new InvalidOperationException("No active console session found. This typically occurs when no user is logged in.");


			HANDLE phToken = HANDLE.Null;
			
			if (!PInvoke.WTSQueryUserToken(sessionId, ref phToken))
			{
				int error = Marshal.GetLastWin32Error();
				
				if (error is 2 or 87 or 7022)
						throw new InvalidOperationException($"The session ID {sessionId} does not exist");
				
				throw new Interop.Win32Exception(Marshal.GetLastWin32Error(), $"Failed to query user token for session {sessionId}");
			}
			
			SafeAccessTokenHandle hSessionUserToken = new SafeAccessTokenHandle(phToken);
			// if (!Wtsapi32.WTSQueryUserToken(sessionId, out var hSessionUserToken))
			// {
			// 	int error = Marshal.GetLastWin32Error();
			//
			// 	if (error is 2 or 87 or 7022)
			// 		throw new InvalidOperationException($"The session ID {sessionId} does not exist");
			//
			// 	throw new Interop.Win32Exception(Marshal.GetLastWin32Error(), $"Failed to query user token for session {sessionId}");
			// }


			// todo: investigate if this using causes issues with the returned handle / make DuplicateTokenAsPrimary handle disposal
			using (hSessionUserToken)
			{
				// First see if the token is the full token or not. If it is a limited token we need to get the
				// linked (full/elevated token) and use that for the CreateProcess task. If it is already the full or
				// default token then we already have the best token possible.
				var elevationType = GetTokenElevationType(hSessionUserToken);
				if (elevationType == TOKEN_ELEVATION_TYPE.TokenElevationTypeLimited && elevated == true)
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
