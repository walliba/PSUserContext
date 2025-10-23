using PSUserContext.Api.Helpers;
using PSUserContext.Api.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static PSUserContext.Api.Native.Wtsapi32;

namespace PSUserContext.Api.Services
{
	public class SessionInfo
	{
		public uint SessionId { get; private set; }
		public string UserName { get; private set; } = string.Empty;
		public string DomainName { get; private set; } = string.Empty;
		public string SessionName { get; private set; } = string.Empty;
		public SessionState State { get; private set; }
		public override string ToString()
			=> $"{DomainName}\\{UserName} (Session {SessionId}, {State})";

		public SessionInfo(uint sessionId, string? userName, string? domainName, string? sessionName, SessionState state)
		{
			SessionId = sessionId;
			UserName = userName ?? string.Empty;
			DomainName = domainName ?? string.Empty;
			SessionName = sessionName ?? string.Empty;
			State = state;
		}
	}

	public static class SessionExtensions
	{
		public static uint? GetActiveConsoleSessionId()
		{
			uint sessionId = Kernel32.WTSGetActiveConsoleSessionId();

			if (sessionId == INVALID_SESSION_ID)
				return null;

			return sessionId;
		}


		// todo: may or may not implement later
		public static SessionInfo GetSession(string userName, string? domainName = null)
		{
			var sessions = GetSessions();
			return sessions.FirstOrDefault(s =>
				string.Equals(s.UserName, userName, StringComparison.OrdinalIgnoreCase) &&
				(domainName is null || string.Equals(s.DomainName, domainName, StringComparison.OrdinalIgnoreCase)));
		}

		public static List<SessionInfo> GetSessions()
		{
			List<SessionInfo> sessions = new List<SessionInfo>();


			if (!WTSEnumerateSessions(IntPtr.Zero, 0, 1, out SafeWtsMemoryHandle ppSessionInfo, out int count))
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
						var native = Marshal.PtrToStructure<WTS_SESSION_INFO>(recordPtr);
						var userName = Wtsapi32.GetSessionString(native.SessionId, WTS_INFO_CLASS.WTSUserName);
						var domainName = Wtsapi32.GetSessionString(native.SessionId, WTS_INFO_CLASS.WTSDomainName);
						var sessionName = Wtsapi32.GetSessionString(native.SessionId, WTS_INFO_CLASS.WTSWinStationName);

						sessions.Add(new SessionInfo(native.SessionId, userName, domainName, sessionName, native.State));
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
