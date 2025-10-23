using PSUserContext.Api.Native;
using PSUserContext.Api.Services;
using System.Management.Automation;
using static PSUserContext.Api.Native.Wtsapi32;

namespace PSUserContext.Cmdlets
{
	[Cmdlet(VerbsCommon.Get, "UserContext")]
	[OutputType(typeof(SessionInfo))]
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

				WriteObject(s);
			}
		}
	}
}
