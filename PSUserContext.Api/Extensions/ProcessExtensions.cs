using PSUserContext.Api.Helpers;
using PSUserContext.Api.Models;
using PSUserContext.Api.Native;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PSUserContext.Api.Extensions
{
	public static class ProcessExtensions
	{
		public const uint INFINITE = UInt32.MaxValue;

		public static bool CreateProcessAsUser(
			SafeHandle hToken,
			string applicationName,
			StringBuilder commandLine,
			IntPtr processAttributes,
			IntPtr threadAttributes,
			bool inheritHandles,
			ProcessCreationFlags creationFlags,
			SafeEnvironmentBlockHandle environment,
			string currentDirectory,
			ref InteropTypes.STARTUPINFO startupInfo,
			out InteropTypes.PROCESS_INFORMATION processInformation)
		{
			bool result = Advapi32.CreateProcessAsUserW(
				hToken,
				applicationName,
				commandLine,
				processAttributes,
				threadAttributes,
				inheritHandles,
				creationFlags,
				environment,
				currentDirectory,
				ref startupInfo,
				out processInformation);
			return result;
		}
	}
}
