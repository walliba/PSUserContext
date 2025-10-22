using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace PSUserContext.Api.Native
{
	public static class InteropTypes
	{

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
			SW_HIDE = 0,
			SW_SHOWNORMAL = 1,
			SW_NORMAL = 1,
			SW_SHOWMINIMIZED = 2,
			SW_SHOWMAXIMIZED = 3,
			SW_MAXIMIZE = 3,
			SW_SHOWNOACTIVATE = 4,
			SW_SHOW = 5,
			SW_MINIMIZE = 6,
			SW_SHOWMINNOACTIVE = 7,
			SW_SHOWNA = 8,
			SW_RESTORE = 9,
			SW_SHOWDEFAULT = 10,
			SW_MAX = 10
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
			public uint dwFlags;
			public SW wShowWindow;
			public ushort cbReserved2;
			public IntPtr lpReserved2;
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
