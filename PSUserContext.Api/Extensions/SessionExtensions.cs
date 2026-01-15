using PSUserContext.Api.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.RemoteDesktop;

// using static PSUserContext.Api.Interop.Wtsapi32;

namespace PSUserContext.Api.Extensions
{
	public static class SessionExtensions
	{
		internal const uint INVALID_SESSION_ID = 0xFFFFFFFF;
		
		public static bool IsTokenValid(this UserContextInfo ctx)
		{
			if (!PInvoke.WTSQueryUserToken(ctx.Id, out var token))
				return false;
			
			// Ensure handle is closed when exiting scope
			using var _ = token;
			
			return true;
		}
		
		private static bool IsNonInteractiveState(this WtsSessionState state) =>
			state is WtsSessionState.Listen or WtsSessionState.Down or WtsSessionState.Init or WtsSessionState.Reset;

		public static uint? GetConsoleSessionId()
		{
			return GetActiveConsoleSessionId();
		}
		
		internal static uint? GetActiveConsoleSessionId()
		{
			uint sessionId = PInvoke.WTSGetActiveConsoleSessionId();

			if (sessionId == INVALID_SESSION_ID)
				return null;
			
			return sessionId;
		}

		public static UserContextInfo? GetActiveConsoleSession()
		{
			uint? sessionId = GetActiveConsoleSessionId();
			
			if (sessionId == null)
				return null;
			
			return GetSession(sessionId.Value);
		}
		
		public static UserContextInfo? GetSession(uint sessionId)
		{
			var sessions = GetSessions();
			
			return sessions.FirstOrDefault(s => s.Id == sessionId);
		}
		public static UserContextInfo? GetSession(string userName, string? domainName = null)
		{
			var sessions = GetSessions();
			
			return sessions.FirstOrDefault(s =>
				string.Equals(s.UserName, userName, StringComparison.OrdinalIgnoreCase) &&
				(domainName is null || string.Equals(s.DomainName, domainName, StringComparison.OrdinalIgnoreCase)));
		}

		public static unsafe IEnumerable<UserContextInfo> GetSessions()
		{
			List<UserContextInfo> sessions = new List<UserContextInfo>();
			
			if (!PInvoke.WTSEnumerateSessions(HANDLE.WTS_CURRENT_SERVER_HANDLE, 0, 1, out var ppSessionInfo, out uint count))
				throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to enumerate WTS sessions.");

			try
			{
				for (int i = 0; i < count; i++)
				{
					string? userName = PInvoke.WTSQuerySessionString(ppSessionInfo[i].SessionId, WTS_INFO_CLASS.WTSUserName);
					string? domainName = PInvoke.WTSQuerySessionString(ppSessionInfo[i].SessionId, WTS_INFO_CLASS.WTSDomainName);
					string? sessionName = PInvoke.WTSQuerySessionString(ppSessionInfo[i].SessionId, WTS_INFO_CLASS.WTSWinStationName);
					
					sessions.Add(new UserContextInfo()
					{
						Id = ppSessionInfo[i].SessionId,
						UserName = userName,
						DomainName = domainName,
						SessionName = sessionName,
						State = (int)ppSessionInfo[i].State
					});

				}
			}
			finally
			{
				PInvoke.WTSFreeMemory(ppSessionInfo);
			}
			
			return sessions;
		}
	}
}
