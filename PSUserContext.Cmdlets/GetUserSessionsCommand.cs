using PSUserContext.Api.Native;
using PSUserContext.Api.Services;
using System.Management.Automation;
using static PSUserContext.Api.Native.Wtsapi32;

namespace PSUserContext.Cmdlets
{
	[Cmdlet(VerbsCommon.Get, "UserSessions")]
	[OutputType(typeof(UserSessionInfo))]
	public sealed class GetUserSessionsCommand : PSCmdlet
	{
		protected override void ProcessRecord()
		{

			var sessions = WTSExtensions.GetSessions();

			foreach (var native in sessions)
			{
				// Skip listening sessions
				if (native.State == WTS_CONNECTSTATE_CLASS.Listen)
					continue;
				string userName = Wtsapi32.GetSessionString(native.SessionId, WTS_INFO_CLASS.WTSUserName) ?? string.Empty;
				string domainName = Wtsapi32.GetSessionString(native.SessionId, WTS_INFO_CLASS.WTSDomainName) ?? string.Empty;

				// Skip sessions with no user
				if (string.IsNullOrEmpty(userName) && string.IsNullOrEmpty(domainName))
					continue;

				WriteObject(new UserSessionInfo
				{
					SessionId = native.SessionId,
					UserName = userName,
					DomainName = domainName,
					State = native.State
				});
			}
		}
	}

	// ---------------------------------------------------------------------
	// Supporting model type
	// ---------------------------------------------------------------------
	public sealed class UserSessionInfo
	{
		public uint SessionId { get; set; }
		public string UserName { get; set; } = string.Empty;
		public string DomainName { get; set; } = string.Empty;
		public WTS_CONNECTSTATE_CLASS State { get; set; }

		public override string ToString()
			=> $"{DomainName}\\{UserName} (Session {SessionId}, {State})";
	}
}
