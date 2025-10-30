using PSUserContext.Api.Helpers;
using System;
using System.Runtime.InteropServices;

namespace PSUserContext.Api.Interop
{
	internal static class Userenv
	{
		private const string DllName = "Userenv.dll";
		
		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool CreateEnvironmentBlock(
			out SafeEnvironmentBlockHandle lpEnvironment,
			SafeHandle hToken,
			[MarshalAs(UnmanagedType.Bool)] bool bInherit);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool DestroyEnvironmentBlock(
			IntPtr lpEnvironment);
	}
}
