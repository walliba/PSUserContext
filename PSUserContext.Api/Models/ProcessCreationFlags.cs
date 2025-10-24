using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSUserContext.Api.Models
{
	/// <summary>
	/// Flags controlling process creation behavior.
	/// Corresponds to CREATE_* constants.
	/// </summary>
	[Flags]
	public enum ProcessCreationFlags : uint
	{
		None = 0x00000000,
		DebugProcess = 0x00000001,                // DEBUG_PROCESS
		DebugOnlyThisProcess = 0x00000002,        // DEBUG_ONLY_THIS_PROCESS
		CreateSuspended = 0x00000004,             // CREATE_SUSPENDED
		CreateNewConsole = 0x00000010,            // CREATE_NEW_CONSOLE
		CreateNewProcessGroup = 0x00000200,       // CREATE_NEW_PROCESS_GROUP
		CreateUnicodeEnvironment = 0x00000400,    // CREATE_UNICODE_ENVIRONMENT
		CreateSeparateWowVdm = 0x00000800,        // CREATE_SEPARATE_WOW_VDM
		CreateSharedWowVdm = 0x00001000,          // CREATE_SHARED_WOW_VDM
		CreateProtectedProcess = 0x00040000,      // CREATE_PROTECTED_PROCESS
		CreateBreakawayFromJob = 0x01000000,      // CREATE_BREAKAWAY_FROM_JOB
		CreatePreserveCodeAuthzLevel = 0x02000000,// CREATE_PRESERVE_CODE_AUTHZ_LEVEL
		CreateDefaultErrorMode = 0x04000000,      // CREATE_DEFAULT_ERROR_MODE
		CreateNoWindow = 0x08000000,              // CREATE_NO_WINDOW
	}
}
