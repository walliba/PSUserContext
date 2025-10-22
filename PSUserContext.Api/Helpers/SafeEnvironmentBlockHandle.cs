using Microsoft.Win32.SafeHandles;
using PSUserContext.Api.Native;
using System;
using System.Collections.Generic;
using System.Text;

namespace PSUserContext.Api.Helpers
{
	/// <summary>
	/// Represents a handle to an environment block created by CreateEnvironmentBlock.
	/// Automatically frees the block using DestroyEnvironmentBlock when disposed.
	/// </summary>
	public sealed class SafeEnvironmentBlockHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		public SafeEnvironmentBlockHandle() : base(true) { }

		public SafeEnvironmentBlockHandle(IntPtr handle, bool ownsHandle = true) : base(ownsHandle)
		{
			SetHandle(handle);
		}

		protected override bool ReleaseHandle()
		{
			if (IsInvalid)
				return true;

			//Console.WriteLine("Releasing EnvBlock handle: {0}", handle.ToString("X"));
			// Always call DestroyEnvironmentBlock to release memory.
			return Userenv.DestroyEnvironmentBlock(handle);
		}
	}
}
