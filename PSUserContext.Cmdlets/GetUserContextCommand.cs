using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using PSUserContext.Api.Models;
using PSUserContext.Api.Extensions;
using System.Management.Automation;
using PSUserContext.Api.Interop;

namespace PSUserContext.Cmdlets
{
	[Cmdlet(VerbsCommon.Get,"UserContext", DefaultParameterSetName = "ById")]
	[OutputType(typeof(WtsSessionInfo))]
	[OutputType(typeof(ErrorRecord))]
	public sealed class GetUserContextCommand : PSCmdlet
	{
		private const string ById = "ById";
		private const string ByUser = "ByUser";
		
		[Parameter(Position = 0, ParameterSetName = ById)]
		[Alias("Id")]
		public uint SessionId { get; set; } = InteropTypes.INVALID_SESSION_ID;

		[Parameter(Position = 0, ParameterSetName = ByUser)]
		[Alias("User")]
		public string UserName { get; set; } = String.Empty;
		protected override void ProcessRecord()
		{
			// If no session is supplied, list them
			if (ParameterSetName == ById)
			{
				if (SessionId == InteropTypes.INVALID_SESSION_ID)
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

					return;
				}
				
				var found = SessionExtensions.GetSession(SessionId);
				
				if (found is not null)
					WriteObject(found);
				else
					WriteError(new ErrorRecord(
						new ItemNotFoundException($"No sessions found matching ID {SessionId}"),
						"SessionNotFound",
						ErrorCategory.ObjectNotFound,
						SessionId));
			}

			if (ParameterSetName == ByUser)
			{
				var found = SessionExtensions.GetSession(UserName);
				
				if (found is not null)
					WriteObject(found);
				else
					WriteError(new ErrorRecord(
						new ItemNotFoundException($"No sessions found for user '{UserName}'"),
						"SessionNotFound",
						ErrorCategory.ObjectNotFound,
						UserName));
			}
				
		}
	}
}
