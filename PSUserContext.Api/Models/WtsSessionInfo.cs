using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PSUserContext.Api.Interop.Wtsapi32;

namespace PSUserContext.Api.Models
{
	public class WtsSessionInfo
	{
		public uint SessionId { get; private set; }
		public string UserName { get; private set; } = string.Empty;
		public string DomainName { get; private set; } = string.Empty;
		public string SessionName { get; private set; } = string.Empty;
		public WtsSessionState State { get; private set; }
		public override string ToString()
			=> $"{DomainName}\\{UserName} (Session {SessionId}, {State})";

		public WtsSessionInfo(uint sessionId, string? userName, string? domainName, string? sessionName, WtsSessionState state)
		{
			SessionId = sessionId;
			UserName = userName ?? string.Empty;
			DomainName = domainName ?? string.Empty;
			SessionName = sessionName ?? string.Empty;
			State = state;
		}

		public string? GetDownLevelName(bool fallback = false)
		{
			if (string.IsNullOrEmpty(DomainName))
			{
				if (fallback)
					return UserName;

				return null;
			}
			else
			{
				return $"{DomainName}\\{UserName}";
			}
		}
	}
}
