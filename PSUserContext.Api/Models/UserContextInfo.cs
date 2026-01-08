using System.Collections.Generic;
using PSUserContext.Api.Extensions;

namespace PSUserContext.Api.Models
{
	public class UserContextInfo
	{
		public string? DomainName { get; init; }
		public string? UserName { get; init; }
		public required uint Id { get; init; }
		public string? SessionName { get; init; }
		public required int State { get; init; }
		
		public static implicit operator uint(UserContextInfo sessionInfo)
			=> sessionInfo.Id;
		
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
