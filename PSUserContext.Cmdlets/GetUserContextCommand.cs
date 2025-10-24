using PSUserContext.Api.Models;
using PSUserContext.Api.Extensions;
using System.Management.Automation;

namespace PSUserContext.Cmdlets
{
	[Cmdlet(VerbsCommon.Get, "UserContext")]
	[OutputType(typeof(WtsSessionInfo))]
	public sealed class GetUserContextCommand : PSCmdlet
	{
		protected override void ProcessRecord()
		{

			var sessions = SessionExtensions.GetSessions();

			foreach (var s in sessions)
			{
				// Skip listening sessions
				if (s.State == WtsSessionState.Listen)
					continue;

				// Skip sessions with no user
				if (string.IsNullOrEmpty(s.UserName) && string.IsNullOrEmpty(s.DomainName))
					continue;

				WriteObject(s);
			}
		}
	}
}
