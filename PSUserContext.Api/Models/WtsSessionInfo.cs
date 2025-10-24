using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PSUserContext.Api.Extensions;
using static PSUserContext.Api.Interop.Wtsapi32;

namespace PSUserContext.Api.Models
{
	public class WtsSessionInfo(
		uint id,
		string? userName,
		string? domainName,
		string? sessionName,
		WtsSessionState state)
	{
		public string DomainName { get; init; } = domainName ?? string.Empty;
		public string UserName { get; init; } = userName ?? string.Empty;
		public uint Id { get; init; } = id;
		public string SessionName { get; init; } = sessionName ?? string.Empty;
		public WtsSessionState State { get; private set; } = state;

		public override string ToString()
			=> $"{DomainName}\\{UserName} (Session {Id}, {State})";

		public string? GetEnvironmentVariable(string variableName)
		{
			return EnvExtensions.GetVariable(this.Id, variableName);
		}
		
		public Dictionary<string, string> GetEnvironment()
		{
			return EnvExtensions.GetVariables(this.Id);
		}
		public string? GetDownLevelName(bool fallback = false)
		{
			if (string.IsNullOrEmpty(DomainName))
			{
				return fallback ? UserName : null;
			}
			else
			{
				return $"{DomainName}\\{UserName}";
			}
		}
	}
}
