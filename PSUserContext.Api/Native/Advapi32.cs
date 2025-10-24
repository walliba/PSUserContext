using Microsoft.Win32.SafeHandles;
using PSUserContext.Api.Helpers;
using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using static PSUserContext.Api.Native.InteropTypes;

namespace PSUserContext.Api.Native
{
	internal static class Advapi32
	{
		private const string DllName = "advapi32.dll";

		[DllImport(DllName, SetLastError = true)]
		internal static extern bool DuplicateTokenEx(
			SafeHandle ExistingTokenHandle,
			uint dwDesiredAccess,
			IntPtr lpThreadAttributes,
			SECURITY_IMPERSONATION_LEVEL ImpersonationLevel,
			TOKEN_TYPE TokenType,
			out SafeNativeHandle DuplicateTokenHandle);

		[DllImport(DllName, CharSet = CharSet.Unicode, SetLastError = true)]
		internal static extern bool CreateProcessAsUserW(
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

		[DllImport(DllName, SetLastError = true)]
		internal static extern bool GetTokenInformation(
			SafeHandle TokenHandle,
			uint TokenInformationClass,
			SafeHGlobalBuffer TokenInformation,
			uint TokenInformationLength,
			out uint ReturnLength);

		[DllImport(DllName, CharSet = CharSet.Unicode, SetLastError = true)]
		internal static extern bool LookupPrivilegeName(
			string? lpSystemName,
			ref LUID lpLuid,
			StringBuilder? lpName,
			ref uint cchName);

		[DllImport(DllName, SetLastError = true)]
		internal static extern bool OpenProcessToken(
		   SafeHandle ProcessHandle,
		   TokenAccessLevels DesiredAccess,
		   out SafeAccessTokenHandle TokenHandle);
	}
}
