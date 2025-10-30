using Microsoft.Win32.SafeHandles;
using PSUserContext.Api.Interop;
using System;
using System.Collections.Generic;
using System.Text;

namespace PSUserContext.Api.Helpers
{
	public sealed class SafeNativeHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		public SafeNativeHandle() : base(true) { }

		public SafeNativeHandle(IntPtr handle, bool ownsHandle = true) : base(ownsHandle) 
		{
			SetHandle(handle);
		}

		protected override bool ReleaseHandle()
		{
			if (IsInvalid)
				return true;

			//Console.WriteLine("Releasing Native handle: {0}", handle.ToString("X"));

			return Kernel32.CloseHandle(handle);
		}
	}
}
