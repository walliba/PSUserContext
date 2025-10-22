using Microsoft.Win32.SafeHandles;
using PSUserContext.Api.Helpers;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace PSUserContext.Api.Native
{
	public static class Userenv
	{
		[DllImport("userenv.dll", SetLastError = true)]
		public static extern bool CreateEnvironmentBlock(
			out SafeEnvironmentBlockHandle lpEnvironment,
			SafeHandle hToken,
			[MarshalAs(UnmanagedType.Bool)] bool bInherit);

		[DllImport("userenv.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool DestroyEnvironmentBlock(
			IntPtr lpEnvironment);
	}
}
