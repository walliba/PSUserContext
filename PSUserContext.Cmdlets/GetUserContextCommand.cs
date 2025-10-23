using PSUserContext.Api.Native;
using PSUserContext.Api.Services;
using System.Management.Automation;
using static PSUserContext.Api.Native.Wtsapi32;

namespace PSUserContext.Cmdlets
{
	[Cmdlet(VerbsCommon.Get, "UserContext")]
	[OutputType(typeof(UserSessionInfo))]
	public sealed class GetUserContextCommand : PSCmdlet
	{
		protected override void ProcessRecord()
		{

			var sessions = SessionExtensions.GetSessions();

			foreach (var s in sessions)
			{
				// Skip listening sessions
				if (s.State == Wtsapi32.SessionState.Listen)
					continue;

				// Skip sessions with no user
				if (string.IsNullOrEmpty(s.UserName) && string.IsNullOrEmpty(s.DomainName))
					continue;

				WriteObject(new UserSessionInfo
				{
					SessionId = s.SessionId,
					UserName = s.UserName,
					DomainName = s.DomainName,
					SessionName = s.SessionName,
					State = s.State
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
		public string SessionName { get; set; } = string.Empty;
		public Wtsapi32.SessionState State { get; set; }

		public override string ToString()
			=> $"{DomainName}\\{UserName} (Session {SessionId}, {State})";
	}
}
