using System;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace PSUserContext.Api
{
	public class UserContextRunner
	{
		// Todo: abstract implementation
		public void RunScriptInUserContext(string script, int? sessionId, bool captureOutput)
		{
			// 1. Enumerate sessions via WTSEnumerateSessions
			// 2. Use WTSQueryUserToken(sessionId)
			// 3. Duplicate token with DuplicateTokenEx
			// 4. Adjust privileges (SeAssignPrimaryTokenPrivilege, SeIncreaseQuotaPrivilege)
			// 5. Create environment block
			// 6. CreateProcessAsUser with redirected pipes
			// 7. Capture output streams if requested
			// 8. Cleanup: close handles, destroy environment block
		}
	}
}
