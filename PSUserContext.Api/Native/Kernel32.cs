using Microsoft.Win32.SafeHandles;
using PSUserContext.Api.Helpers;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using static PSUserContext.Api.Native.InteropTypes;

namespace PSUserContext.Api.Native
{
	public static class Kernel32
	{
		private const string DllName = "kernel32.dll";

		public const uint INFINITE = UInt32.MaxValue;

		[DllImport(DllName, SetLastError = true)]
		public static extern uint WaitForSingleObject(
			IntPtr hHandle,
			uint dwMilliseconds);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool CloseHandle(IntPtr hObject);

		[DllImport(DllName)]
		public static extern uint WTSGetActiveConsoleSessionId();

		[DllImport(DllName)]
		public static extern SafeProcessHandle GetCurrentProcess();

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool CreatePipe(
			out SafeFileHandle hReadPipe,
			out SafeFileHandle hWritePipe,
			ref SECURITY_ATTRIBUTES lpPipeAttributes,
			uint nSize);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool SetHandleInformation(
			SafeHandle hObject,
			uint dwMask,
			uint dwFlags);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool ReadFile(
			SafeHandle hFile,
			byte[] lpBuffer,
			int nNumberOfBytesToRead,
			ref uint lpNumberOfBytesRead,
			IntPtr lpOverlapped); // Usually IntPtr.Zero for synchronous I/O

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool PeekNamedPipe(
			SafeHandle hNamedPipe,
			byte[] lpBuffer,
			uint nBufferSize,
			ref uint lpBytesRead,
			ref uint lpTotalBytesAvail,
			ref uint lpBytesLeftThisMessage);

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool DuplicateHandle(
			SafeHandle hSourceProcessHandle,
			SafeHandle hSourceHandle,
			SafeHandle hTargetProcessHandle, 
			out SafeNativeHandle lpTargetHandle,
			uint dwDesiredAccess, 
			[MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, 
			uint dwOptions);
	}
}
