using Microsoft.Win32.SafeHandles;
using PSUserContext.Api.Interop;
using System;
using System.Collections.Generic;
using System.Text;

namespace PSUserContext.Api.Helpers
{
	public sealed class SafeWtsMemoryHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		public SafeWtsMemoryHandle() : base(true) { }

		public SafeWtsMemoryHandle(IntPtr handle, bool ownsHandle = true) : base(ownsHandle)
		{
			SetHandle(handle);
		}

		protected override bool ReleaseHandle()
		{
			if (IsInvalid)
				return true;

			//Console.WriteLine("Releasing WTS handle: {0}", handle.ToString("X"));

			Wtsapi32.WTSFreeMemory(handle);
			return true;
		}
	}
}
