using Microsoft.Win32.SafeHandles;
using PSUserContext.Api.Helpers;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using static PSUserContext.Api.Native.InteropTypes;

namespace PSUserContext.Api.Native
{
	public static class Advapi32
	{
		[DllImport("advapi32.dll", SetLastError = true)]
		public static extern bool DuplicateTokenEx(
			SafeHandle ExistingTokenHandle,
			uint dwDesiredAccess,
			IntPtr lpThreadAttributes,
			SECURITY_IMPERSONATION_LEVEL ImpersonationLevel,
			TOKEN_TYPE TokenType,
			out SafeNativeHandle DuplicateTokenHandle);

		[DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern bool CreateProcessAsUserW(
			SafeHandle hToken,
			string lpApplicationName,
			StringBuilder lpCommandLine,
			IntPtr lpProcessAttributes,
			IntPtr lpThreadAttributes,
			[MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
			ProcessCreationFlags dwCreationFlags,
			SafeHandle lpEnvironment,
			string lpCurrentDirectory,
			ref STARTUPINFO lpStartupInfo,
			out PROCESS_INFORMATION lpProcessInformation);

		[DllImport("advapi32.dll", SetLastError = true)]
		public static extern bool GetTokenInformation(
			SafeHandle TokenHandle,
			uint TokenInformationClass,
			SafeHGlobalBuffer TokenInformation,
			uint TokenInformationLength,
			out uint ReturnLength);

		[DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern bool LookupPrivilegeName(
			string? lpSystemName,
			ref LUID lpLuid,
			StringBuilder? lpName,
			ref uint cchName);

		[DllImport("advapi32.dll", SetLastError = true)]
		public static extern bool OpenProcessToken(
		   SafeHandle ProcessHandle,
		   TokenAccessLevels DesiredAccess,
		   out SafeAccessTokenHandle TokenHandle);
	}
}
