using PSUserContext.Api.Helpers;
using PSUserContext.Api.Native;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Text;
using static PSUserContext.Api.Native.Wtsapi32;

namespace PSUserContext.Cmdlet
{
	//[Cmdlet(VerbsCommon.Get, "LoggedOnUser")]
	//[OutputType(typeof(UserSessionInfo))]
	//public sealed class GetLoggedOnUserCommand : PSCmdlet
	//{
	//	protected override void ProcessRecord()
	//	{
	//		SafeWtsMemoryHandle ppSessionInfo;
	//		int count = 0;

	//		try
	//		{
	//			if (!Wtsapi32.WTSEnumerateSessions(IntPtr.Zero, 0, 1, out ppSessionInfo, out count))
	//				throw new Win32Exception(Marshal.GetLastWin32Error(), "WTSEnumerateSessions failed.");

	//			int dataSize = Marshal.SizeOf<WTS_SESSION_INFO>();

	//			for (int i = 0; i < count; i++)
	//			{
	//				IntPtr recordPtr = ppSessionInfo + i * dataSize;
	//				var native = Marshal.PtrToStructure<WTS_SESSION_INFO>(recordPtr);

	//				// Skip disconnected sessions
	//				if (native.State != WTS_CONNECTSTATE_CLASS.Active)
	//					continue;

	//				string userName = Wtsapi32.GetSessionString(native.SessionId, WTS_INFO_CLASS.WTSUserName);
	//				string domainName = Wtsapi32.GetSessionString(native.SessionId, WTS_INFO_CLASS.WTSDomainName);

	//				WriteObject(new UserSessionInfo
	//				{
	//					SessionId = native.SessionId,
	//					UserName = userName ?? string.Empty,
	//					DomainName = domainName ?? string.Empty,
	//					State = native.State
	//				});
	//			}
	//		}
	//		finally
	//		{
	//			if (ppSessionInfo != IntPtr.Zero)
	//				Wtsapi32.WTSFreeMemory(ppSessionInfo);
	//		}
	//	}
	//}

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
