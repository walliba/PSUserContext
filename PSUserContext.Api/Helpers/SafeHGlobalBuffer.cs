using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace PSUserContext.Api.Helpers
{
	public sealed class SafeHGlobalBuffer : SafeHandleZeroOrMinusOneIsInvalid
	{
		public static readonly SafeHGlobalBuffer Null = new SafeHGlobalBuffer(IntPtr.Zero, ownsHandle: false);

		// If ownsHandle = true the SafeHandle will free the pointer.
		public SafeHGlobalBuffer(uint cb) : base(true)
		{
			if (cb < 0) throw new ArgumentOutOfRangeException(nameof(cb));
			// AllocHGlobal throws on failure; SetHandle is fine afterwards.
			SetHandle(Marshal.AllocHGlobal((IntPtr)cb));
		}

		public SafeHGlobalBuffer(IntPtr handle, bool ownsHandle = true) : base(ownsHandle)
		{
			SetHandle(handle);
		}

		protected override bool ReleaseHandle()
		{
			// FreeHGlobal on a zero handle is a no-op, but be explicit.
			if (IsInvalid) return true;

			//Console.WriteLine("Releasing HGlobal handle: {0}", handle.ToString("X"));

			try
			{
				Marshal.FreeHGlobal(handle);
				// Mark handle as invalid to avoid double-free
				return true;
			}
			catch
			{
				// Returning false signals the runtime that release failed.
				return false;
			}
		}
	}
}
