using PSUserContext.Api.Helpers;
using PSUserContext.Api.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PSUserContext.Api.Extensions
{
	public static class EnvExtensions
	{
		public static SafeEnvironmentBlockHandle CreateEnvironmentBlock(SafeHandle hToken, bool inherit = false)
		{
			if (!Native.Userenv.CreateEnvironmentBlock(out SafeEnvironmentBlockHandle environment, hToken, inherit))
				throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create environment block.");

			if (environment == null || environment.IsInvalid)
				throw new InvalidOperationException("Failed to create environment for specified user.");

			return environment;
		}
	}
}
