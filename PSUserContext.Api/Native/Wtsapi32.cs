using PSUserContext.Api.Helpers;
using System;
using System.Runtime.InteropServices;

namespace PSUserContext.Api.Native
{
	internal static class Wtsapi32
	{
		private const string DllName = "wtsapi32.dll";

		internal static readonly IntPtr WTS_CURRENT_SERVER_HANDLE = IntPtr.Zero;

		internal enum WTS_INFO_CLASS
		{
			WTSInitialProgram,
			WTSApplicationName,
			WTSWorkingDirectory,
			WTSOEMId,
			WTSSessionId,
			WTSUserName,
			WTSWinStationName,
			WTSDomainName,
			WTSConnectState,
			WTSClientBuildNumber,
			WTSClientName,
			WTSClientDirectory,
			WTSClientProductId,
			WTSClientHardwareId,
			WTSClientAddress,
			WTSClientDisplay,
			WTSClientProtocolType,
			WTSIdleTime,
			WTSLogonTime,
			WTSIncomingBytes,
			WTSOutgoingBytes,
			WTSIncomingFrames,
			WTSOutgoingFrames,
			WTSClientInfo,
			WTSSessionInfo,
			WTSSessionInfoEx,
			WTSConfigInfo,
			WTSValidationInfo,
			WTSSessionAddressV4,
			WTSIsRemoteSession,
			WTSSessionActivityId
		}

		[StructLayout(LayoutKind.Sequential)]
		internal readonly struct WTS_SESSION_INFO
		{
			public readonly uint SessionId;
			[MarshalAs(UnmanagedType.LPWStr)] public readonly string pWinStationName;
			public readonly Models.WtsSessionState State;
		}

		[DllImport(DllName, CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool WTSEnumerateSessions(
			IntPtr hServer,
			int Reserved,
			int Version,
			out SafeWtsMemoryHandle ppSessionInfo,
			out int pCount);

		[DllImport(DllName, SetLastError = true)]
		internal static extern void WTSFreeMemory(IntPtr pMemory);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool WTSQueryUserToken(
			uint SessionId,
			out SafeNativeHandle phToken);

		[DllImport(DllName, CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool WTSQuerySessionInformation(
			IntPtr hServer,
			uint SessionId,
			WTS_INFO_CLASS WTSInfoClass,
			out SafeWtsMemoryHandle ppBuffer,
			out uint pBytesReturned);

		public static string? GetSessionString(uint sessionId, WTS_INFO_CLASS infoClass)
		{
			if (!WTSQuerySessionInformation(
					IntPtr.Zero, sessionId, infoClass,
					out var buffer, out var bytesReturned))
				return null;

			using (buffer)
			{
				if (buffer.IsInvalid || bytesReturned == 0)
					return null;

				return Marshal.PtrToStringUni(buffer.DangerousGetHandle());
			}
		}
	}
}
