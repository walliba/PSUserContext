using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using PSUserContext.Api.Models;
using PSUserContext.Api.Extensions;
using System.Management.Automation;
using PSUserContext.Api.Interop;

namespace PSUserContext.Cmdlets
{
	/// <summary>
	/// Get-UserContext allows you to query sessions on the local computer.
	/// </summary>
	/// <remarks>
	/// This is similar to the native `quser` command.
	/// </remarks>
	[Cmdlet(VerbsCommon.Get,"UserContext")]
	[OutputType(typeof(UserContextInfo))]
	public sealed class GetUserContextCommand : PSCmdlet
	{
		/// <summary>
		/// Filters results by session ID.
		/// </summary>
		[Parameter(ValueFromPipelineByPropertyName = true)]
		[ValidateRange(0, uint.MaxValue)]
		[Alias("id")]
		public uint? SessionId { get; set; } = null;


		/// <summary>
		/// Filters results by the user's SamAccountName.
		/// </summary>
		/// <remarks>
		/// Wildcards are supported. Matching is case-insensitive.
		/// </remarks>
		[Parameter(Position = 0, ValueFromPipelineByPropertyName = true)]
		[Alias("name")]
		public string? UserName { get; set; } = null;
		
		/// <summary>
		/// Filters results by the user's domain name.
		/// </summary>
		/// <remarks>
		/// Wildcards are supported. Matching is case-insensitive.
		/// </remarks>
		[Parameter(ValueFromPipelineByPropertyName = true)]
		[Alias("domain")]
		public string? DomainName { get; set; } = null;
		
		/// <summary>
		/// When specified, returns only the active console session.
		/// </summary>
		[Parameter]
		public SwitchParameter Console { get; set; }
		
		private IEnumerable<UserContextInfo>? _userContexts;
		protected override void BeginProcessing()
		{
			// Only return sessions that we can obtain a session token from
			_userContexts = SessionExtensions.GetSessions().Where(s => s.IsTokenEligible());
			if (_userContexts is null)
				ThrowTerminatingError(new ErrorRecord(
					new ItemNotFoundException("No sessions were found."),
					"SessionListNull",
					ErrorCategory.ObjectNotFound,
					null));
		}
		
		protected override void ProcessRecord()
		{
			if (Console.IsPresent)
			{
				var session = SessionExtensions.GetActiveConsoleSession();

				if (session is null)
				{
					WriteError(new ErrorRecord(
						new ItemNotFoundException($"No active console session found."),
						"SessionNotFound",
						ErrorCategory.ObjectNotFound,
						null));
				}
				else
				{
					WriteObject(session);
				}
				
				return;
			}
			
			IEnumerable<UserContextInfo> query = _userContexts!;
			
			if (MyInvocation.BoundParameters.ContainsKey(nameof(SessionId)))
			{
				query = query.Where(session => session.Id == SessionId);
			}

			if (MyInvocation.BoundParameters.ContainsKey(nameof(UserName)))
			{
				var pattern = WildcardPattern.Get(UserName, WildcardOptions.IgnoreCase);
				query = query.Where(session => session.UserName is not null && pattern.IsMatch(session.UserName));
			}

			if (MyInvocation.BoundParameters.ContainsKey(nameof(DomainName)))
			{
				var pattern = WildcardPattern.Get(DomainName, WildcardOptions.IgnoreCase);
				query = query.Where(session => session.DomainName is not null && pattern.IsMatch(session.DomainName));
			}
			
			if (!query.Any())
				ThrowTerminatingError(new ErrorRecord(
					new ItemNotFoundException("No eligible user sessions match the specified criteria."),
					"SessionNotFound",
					ErrorCategory.ObjectNotFound,
					_userContexts));
			else
				WriteObject(query, true);
		}
	}
}
