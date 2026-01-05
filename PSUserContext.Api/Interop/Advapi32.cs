using Microsoft.Win32.SafeHandles;
using PSUserContext.Api.Helpers;
using PSUserContext.Api.Models;
using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using static PSUserContext.Api.Interop.InteropTypes;

namespace PSUserContext.Api.Interop
{
	internal static class Advapi32
	{
		private const string DllName = "advapi32.dll";

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool DuplicateTokenEx(
			SafeHandle hExistingTokenHandle,
			uint dwDesiredAccess,
			IntPtr lpTokenAttributes, // todo: make sure passing null/IntPtr.Zero is the best option
			SECURITY_IMPERSONATION_LEVEL ImpersonationLevel,
			TOKEN_TYPE TokenType,
			out SafeAccessTokenHandle phNewToken);

		[DllImport(DllName, CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool CreateProcessAsUser(
			SafeHandle hToken,
			string lpApplicationName,
			StringBuilder lpCommandLine,
			IntPtr lpProcessAttributes,
			IntPtr lpThreadAttributes,
			[MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
			uint dwCreationFlags,
			SafeHandle lpEnvironment,
			string lpCurrentDirectory,
			ref STARTUPINFO lpStartupInfo,
			out PROCESS_INFORMATION lpProcessInformation);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool GetTokenInformation(
			SafeHandle TokenHandle,
			TOKEN_INFORMATION_CLASS TokenInformationClass,
			SafeHGlobalBuffer TokenInformation,
			uint TokenInformationLength,
			out uint ReturnLength);

		[DllImport(DllName, CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool LookupPrivilegeName(
			string? lpSystemName,
			in LUID lpLuid,
			StringBuilder lpName,
			ref uint cchName);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool OpenProcessToken(
		   SafeHandle ProcessHandle,
		   TokenAccessLevels DesiredAccess,
		   out SafeAccessTokenHandle TokenHandle);
	}
}
