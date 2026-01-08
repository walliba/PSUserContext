using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;

namespace PSUserContext.Api.Extensions
{
	public static class TokenExtensions
	{
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
					throw new System.ComponentModel.Win32Exception(error, $"GetTokenInformation({infoClass}) failed to query buffer size.");
			}
			
			if (needLength == 0)
				throw new InvalidOperationException($"TokenInformation returned zero needed size for {infoClass}");


			Span<byte> buffer = new Span<byte>(new byte[needLength]);
			
			if (!PInvoke.GetTokenInformation(hToken, infoClass, buffer, out _))
				throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), $"GetTokenInformation({infoClass}) failed.");
			
			return buffer;
		}

		private static SafeFileHandle DuplicateTokenAsPrimary(SafeHandle hToken)
		{
			// todo: should I check token privileges here?
			
			if (!Windows.Win32.PInvoke.DuplicateTokenEx(hToken, 0, null, Windows.Win32.Security.SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, Windows.Win32.Security.TOKEN_TYPE.TokenPrimary, out var pDupToken))
				throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Failed to duplicate impersonation token as primary");

			return pDupToken;
		}

		private static unsafe Dictionary<String, TOKEN_PRIVILEGES_ATTRIBUTES> GetTokenPrivileges()
		{
			Dictionary<string, TOKEN_PRIVILEGES_ATTRIBUTES> privileges = new Dictionary<string, TOKEN_PRIVILEGES_ATTRIBUTES>();
			
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
					// todo: read unmanaged array, as this is currently broken
					var info = tokenPrivileges.Privileges[i];
					
					uint needed = 0;
					
					PInvoke.LookupPrivilegeName(null, info.Luid, Span<char>.Empty, ref needed);
					Span<char> buffer = new char[needed];
					uint size = (uint)buffer.Length;
					PInvoke.LookupPrivilegeName(null, info.Luid, buffer, ref size);
					var name = new string(buffer.ToArray());
					Console.WriteLine($"Privilege '{name}': {(uint)info.Attributes}");
					privileges[name] = info.Attributes;
				}
			}

			return privileges;
		}

		public static bool HasTokenPrivilege(string privilege)
		{
			var privileges = GetTokenPrivileges();

			if (!privileges.TryGetValue(privilege, out var attributes))
			{
				Console.WriteLine("No token privileges found.");
				return false;
			}

			return attributes == TOKEN_PRIVILEGES_ATTRIBUTES.SE_PRIVILEGE_ENABLED;
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
			if (sessionId == SessionExtensions.INVALID_SESSION_ID)
				sessionId = SessionExtensions.GetActiveConsoleSessionId()
					?? throw new InvalidOperationException("No active console session found. This typically occurs when no user is logged in.");
			
			
			if (!PInvoke.WTSQueryUserToken(sessionId, out var hSessionUserToken))
			{
				int error = Marshal.GetLastWin32Error();
				
				if (error is 2 or 87 or 7022)
						throw new InvalidOperationException($"The session ID {sessionId} does not exist");
				
				throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), $"Failed to query user token for session {sessionId}");
			}
			
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
				
				return DuplicateTokenAsPrimary(hSessionUserToken);
			}
		}
	}
}
