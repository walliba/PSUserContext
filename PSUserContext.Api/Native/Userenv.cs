using PSUserContext.Api.Helpers;
using System;
using System.Runtime.InteropServices;

namespace PSUserContext.Api.Native
{
	internal static class Userenv
	{
		[DllImport("userenv.dll", SetLastError = true)]
		internal static extern bool CreateEnvironmentBlock(
			out SafeEnvironmentBlockHandle lpEnvironment,
			SafeHandle hToken,
			[MarshalAs(UnmanagedType.Bool)] bool bInherit);

		[DllImport("userenv.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool DestroyEnvironmentBlock(
			IntPtr lpEnvironment);
	}
}
