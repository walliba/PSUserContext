using PSUserContext.Api.Helpers;
using PSUserContext.Api.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static PSUserContext.Api.Native.Wtsapi32;

namespace PSUserContext.Api.Services
{
	public static class WTSExtensions
	{
		public static uint? GetActiveConsoleSessionId()
		{
			uint sessionId = Kernel32.WTSGetActiveConsoleSessionId();

			if (sessionId == INVALID_SESSION_ID)
				return null;

			return sessionId;
		}

		public static List<WTS_SESSION_INFO> GetSessions()
		{
			List<WTS_SESSION_INFO> sessions = new List<WTS_SESSION_INFO>();


			if (!WTSEnumerateSessions(IntPtr.Zero, 0, 1, out SafeWtsMemoryHandle ppSessionInfo, out int count))
				throw new Win32Exception(Marshal.GetLastWin32Error(), "WTSEnumerateSessions failed.");

			using (ppSessionInfo)
			{
				bool success = false;
				try
				{
					ppSessionInfo.DangerousAddRef(ref success);
					IntPtr basePtr = ppSessionInfo.DangerousGetHandle();
					int dataSize = Marshal.SizeOf<WTS_SESSION_INFO>();

					for (int i = 0; i < count; i++)
					{
						IntPtr recordPtr = ppSessionInfo.DangerousGetHandle() + i * dataSize;
						var native = Marshal.PtrToStructure<WTS_SESSION_INFO>(recordPtr);
						sessions.Add(native);
					}
				}
				finally
				{
					if (success)
						ppSessionInfo?.DangerousRelease();
				}
			}

			return sessions;
		}
	}
}
