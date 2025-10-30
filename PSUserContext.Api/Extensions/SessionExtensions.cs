using PSUserContext.Api.Helpers;
using PSUserContext.Api.Models;
using PSUserContext.Api.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static PSUserContext.Api.Interop.Wtsapi32;

namespace PSUserContext.Api.Extensions
{
	public static class SessionExtensions
	{
		public static uint? GetActiveConsoleSessionId()
		{
			uint sessionId = Kernel32.WTSGetActiveConsoleSessionId();

			if (sessionId == InteropTypes.INVALID_SESSION_ID)
				return null;

			return sessionId;
		}
		
		public static WtsSessionInfo? GetSession(uint sessionId)
		{
			var sessions = GetSessions();
			return sessions.FirstOrDefault(s => s.Id == sessionId);
		}
		public static WtsSessionInfo? GetSession(string userName, string? domainName = null)
		{
			var sessions = GetSessions();
			return sessions.FirstOrDefault(s =>
				string.Equals(s.UserName, userName, StringComparison.OrdinalIgnoreCase) &&
				(domainName is null || string.Equals(s.DomainName, domainName, StringComparison.OrdinalIgnoreCase)));
		}

		public static List<WtsSessionInfo> GetSessions()
		{
			List<WtsSessionInfo> sessions = new List<WtsSessionInfo>();
			
			if (!WTSEnumerateSessions(IntPtr.Zero, 0, 1, out var ppSessionInfo, out uint count))
				throw new Win32Exception(Marshal.GetLastWin32Error(), "WTSEnumerateSessions failed.");

			using (ppSessionInfo)
			{
				bool success = false;
				try
				{
					ppSessionInfo.DangerousAddRef(ref success);
					IntPtr basePtr = ppSessionInfo.DangerousGetHandle();
					int dataSize = Marshal.SizeOf<WTS_SESSION_INFO>();

					for (int i = 0; i < count; i++)
					{
						IntPtr recordPtr = ppSessionInfo.DangerousGetHandle() + i * dataSize;
						var sInfo = Marshal.PtrToStructure<WTS_SESSION_INFO>(recordPtr);
						string? userName = Wtsapi32.GetSessionString(sInfo.SessionId, WTS_INFO_CLASS.WTSUserName);
						string? domainName = Wtsapi32.GetSessionString(sInfo.SessionId, WTS_INFO_CLASS.WTSDomainName);
						string? sessionName = Wtsapi32.GetSessionString(sInfo.SessionId, WTS_INFO_CLASS.WTSWinStationName);

						sessions.Add(new WtsSessionInfo()
						{
							Id = sInfo.SessionId,
							UserName = userName,
							DomainName = domainName,
							SessionName = sessionName,
							State =  sInfo.State,
						});
					}
				}
				finally
				{
					if (success)
						ppSessionInfo?.DangerousRelease();
				}
			}

			return sessions;
		}
	}
}
