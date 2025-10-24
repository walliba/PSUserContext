using System;
using System.Runtime.InteropServices;

namespace PSUserContext.Api.Native
{
	public class Win32Exception : System.ComponentModel.Win32Exception
	{
		private readonly string _msg;
		public Win32Exception(string message) : this(Marshal.GetLastWin32Error(), message) { }
		public Win32Exception(int errorCode, string message) : base(errorCode)
		{
			_msg = String.Format("{0} ({1}, Win32ErrorCode {2} - 0x{2:X8})", message, base.Message, errorCode);
		}
		public override string Message { get { return _msg; } }
		public static explicit operator Win32Exception(string message) { return new Win32Exception(message); }
	}

	public static class InteropTypes
	{

		public const uint INVALID_SESSION_ID = 0xFFFFFFFF;

		/// <summary>
		/// Flags for handle inheritance and protection
		/// Corresponds to Win32 HANDLE_FLAGS_* constants
		/// </summary>
		[Flags]
		public enum HandleFlags : uint
		{
			None = 0x00000000,
			Inherit = 0x00000001, // HANDLE_FLAG_INHERIT
			ProtectFromClose = 0x00000002, // HANDLE_FLAG_PROTECT_FROM_CLOSE
		}

		/// <summary>
		/// Flags for STARTUPINFO structure.
		/// Corresponds to Win32 STARTF_* constants
		/// </summary>
		[Flags]
		public enum StartupInfoFlags : uint 
		{
			None = 0x00000000,
			UseShowWindow = 0x00000001,         // STARTF_USESHOWWINDOW
			UseSize = 0x00000002,               // STARTF_USESIZE
			UsePosition = 0x00000004,           // STARTF_USEPOSITION
			UseCountChars = 0x00000008,         // STARTF_USECOUNTCHARS
			UseFillAttribute = 0x00000010,      // STARTF_USEFILLATTRIBUTE
			RunFullScreen = 0x00000020,         // STARTF_RUNFULLSCREEN
			ForceOnFeedback = 0x00000040,       // STARTF_FORCEONFEEDBACK
			ForceOffFeedback = 0x00000080,      // STARTF_FORCEOFFFEEDBACK
			UseStdHandles = 0x00000100,         // STARTF_USESTDHANDLES
			UseHotKey = 0x00000200,             // STARTF_USEHOTKEY
			TitleIsLinkName = 0x00000800,       // STARTF_TITLEISLINKNAME
			TitleIsAppId = 0x00001000,          // STARTF_TITLEISAPPID
			PreventPinning = 0x00002000,        // STARTF_PREVENTPINNING
			UntrustedSource = 0x00008000,       // STARTF_UNTRUSTEDSOURCE
		}

		public enum TOKEN_INFORMATION_CLASS
		{
			TokenUser = 1,
			TokenGroups,
			TokenPrivileges,
			TokenOwner,
			TokenPrimaryGroup,
			TokenDefaultDacl,
			TokenSource,
			TokenType,
			TokenImpersonationLevel,
			TokenStatistics,
			TokenRestrictedSids,
			TokenSessionId,
			TokenGroupsAndPrivileges,
			TokenSessionReference,
			TokenSandBoxInert,
			TokenAuditPolicy,
			TokenOrigin,
			TokenElevationType,
			TokenLinkedToken,
			TokenElevation,
			TokenHasRestrictions,
			TokenAccessInformation,
			TokenVirtualizationAllowed,
			TokenVirtualizationEnabled,
			TokenIntegrityLevel,
			TokenUIAccess,
			TokenMandatoryPolicy,
			TokenLogonSid,
			TokenIsAppContainer,
			TokenCapabilities,
			TokenAppContainerSid,
			TokenAppContainerNumber,
			TokenUserClaimAttributes,
			TokenDeviceClaimAttributes,
			TokenRestrictedUserClaimAttributes,
			TokenRestrictedDeviceClaimAttributes,
			TokenDeviceGroups,
			TokenRestrictedDeviceGroups,
			TokenSecurityAttributes,
			TokenIsRestricted,
			TokenProcessTrustLevel,
			TokenPrivateNameSpace,
			TokenSingletonAttributes,
			TokenBnoIsolation,
			TokenChildProcessFlags,
			TokenIsLessPrivilegedAppContainer,
			TokenIsSandboxed,
			TokenIsAppSilo,
			TokenLoggingInformation,
			TokenLearningMode,
			MaxTokenInfoClass
		}
		public enum SECURITY_IMPERSONATION_LEVEL
		{
			SecurityAnonymous = 0,
			SecurityIdentification = 1,
			SecurityImpersonation = 2,
			SecurityDelegation = 3,
		}

		public enum SW : ushort
		{
			HIDE = 0,
			SHOWNORMAL = 1,
			NORMAL = 1,
			SHOWMINIMIZED = 2,
			SHOWMAXIMIZED = 3,
			MAXIMIZE = 3,
			SHOWNOACTIVATE = 4,
			SHOW = 5,
			MINIMIZE = 6,
			SHOWMINNOACTIVE = 7,
			SHOWNA = 8,
			RESTORE = 9,
			SHOWDEFAULT = 10,
			MAX = 10
		}

		public enum TokenElevationType
		{
			TokenElevationTypeDefault = 1,
			TokenElevationTypeFull,
			TokenElevationTypeLimited,
		}

		public enum TOKEN_TYPE
		{
			TokenPrimary = 1,
			TokenImpersonation = 2
		}

		[Flags]
		public enum PrivilegeAttributes : uint
		{
			Disabled = 0x00000000,
			EnabledByDefault = 0x00000001,
			Enabled = 0x00000002,
			Removed = 0x00000004,
			UsedForAccess = 0x80000000,
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct LUID
		{
			public uint LowPart;
			public int HighPart;
		}
		[StructLayout(LayoutKind.Sequential)]
		public struct LUID_AND_ATTRIBUTES
		{
			public LUID Luid;
			public PrivilegeAttributes Attributes;
		}
		[StructLayout(LayoutKind.Sequential)]
		public struct PROCESS_INFORMATION
		{
			public IntPtr hProcess;
			public IntPtr hThread;
			public int dwProcessId;
			public int dwThreadId;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct STARTUPINFO
		{
			public uint cb;
			[MarshalAs(UnmanagedType.LPWStr)] public string lpReserved;
			[MarshalAs(UnmanagedType.LPWStr)] public string lpDesktop;
			[MarshalAs(UnmanagedType.LPWStr)] public string lpTitle;
			public uint dwX;
			public uint dwY;
			public uint dwXSize;
			public uint dwYSize;
			public uint dwXCountChars;
			public uint dwYCountChars;
			public uint dwFillAttribute;
			public StartupInfoFlags dwFlags;
			public SW wShowWindow;
			public ushort cbReserved2; // Must be zero
			public IntPtr lpReserved2; // Must be IntPtr.Zero
			public IntPtr hStdInput;
			public SafeHandle hStdOutput;
			public SafeHandle hStdError;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct TOKEN_PRIVILEGES
		{
			public int PrivilegeCount;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
			public LUID_AND_ATTRIBUTES[] Privileges;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct SECURITY_ATTRIBUTES
		{
			public Int32 nLength;
			public IntPtr lpSecurityDescriptor;
			public int bInheritHandle;
		}
	}
}
