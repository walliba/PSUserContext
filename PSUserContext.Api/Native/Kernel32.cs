using Microsoft.Win32.SafeHandles;
using PSUserContext.Api.Helpers;
using System;
using System.Runtime.InteropServices;
using static PSUserContext.Api.Native.InteropTypes;

namespace PSUserContext.Api.Native
{
	internal static class Kernel32
	{
		private const string DllName = "kernel32.dll";

		[DllImport(DllName, SetLastError = true)]
		internal static extern uint WaitForSingleObject(
			IntPtr hHandle,
			uint dwMilliseconds);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool CloseHandle(IntPtr hObject);

		[DllImport(DllName)]
		internal static extern uint WTSGetActiveConsoleSessionId();

		[DllImport(DllName)]
		internal static extern SafeProcessHandle GetCurrentProcess();

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool CreatePipe(
			out SafeFileHandle hReadPipe,
			out SafeFileHandle hWritePipe,
			ref SECURITY_ATTRIBUTES lpPipeAttributes,
			uint nSize);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool SetHandleInformation(
			SafeHandle hObject,
			HandleFlags dwMask,
			HandleFlags dwFlags);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool ReadFile(
			SafeHandle hFile,
			byte[] lpBuffer,
			int nNumberOfBytesToRead,
			ref uint lpNumberOfBytesRead,
			IntPtr lpOverlapped); // Usually IntPtr.Zero for synchronous I/O

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool PeekNamedPipe(
			SafeHandle hNamedPipe,
			byte[] lpBuffer,
			uint nBufferSize,
			ref uint lpBytesRead,
			ref uint lpTotalBytesAvail,
			ref uint lpBytesLeftThisMessage);

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool DuplicateHandle(
			SafeHandle hSourceProcessHandle,
			SafeHandle hSourceHandle,
			SafeHandle hTargetProcessHandle, 
			out SafeNativeHandle lpTargetHandle,
			uint dwDesiredAccess, 
			[MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, 
			uint dwOptions);
	}
}
