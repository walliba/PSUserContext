// ReSharper disable CheckNamespace

using System;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Microsoft.Win32.SafeHandles;
using PSUserContext.Api.Interop;
using Win32Exception = System.ComponentModel.Win32Exception;
using winmdroot = global::Windows.Win32;

namespace Windows.Win32;

internal sealed class SafeWtsMemoryHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeWtsMemoryHandle() : base(true) { }

    public SafeWtsMemoryHandle(IntPtr handle, bool ownsHandle = true) : base(ownsHandle)
    {
        SetHandle(handle);
    }

    protected override unsafe bool ReleaseHandle()
    {
        if (IsInvalid)
            return true;

        //Console.WriteLine("Releasing WTS handle: {0}", handle.ToString("X"));

        PInvoke.WTSFreeMemory(handle.ToPointer());
        return true;
    }
}

internal static partial class PInvoke
{
    /// <inheritdoc cref="WTSQueryUserToken(uint, winmdroot.Foundation.HANDLE*)"/>
    internal static unsafe winmdroot.Foundation.BOOL WTSQueryUserToken(uint SessionId,
        out Microsoft.Win32.SafeHandles.SafeFileHandle phTokenSafe)
    {
        winmdroot.Foundation.HANDLE phToken = winmdroot.Foundation.HANDLE.Null;
        winmdroot.Foundation.BOOL __result = PInvoke.WTSQueryUserToken(SessionId, ref phToken);

        phTokenSafe = new Microsoft.Win32.SafeHandles.SafeFileHandle(phToken, ownsHandle: true);
        return __result;
    }
    
    /// <inheritdoc cref="WTSEnumerateSessions(winmdroot.Foundation.HANDLE, uint, uint, winmdroot.System.RemoteDesktop.WTS_SESSION_INFOW**, uint*)"/>
    internal static unsafe winmdroot.Foundation.BOOL WTSEnumerateSessions_SafeHandle([Optional] winmdroot.Foundation.HANDLE hServer, uint Reserved, uint Version, out SafeWtsMemoryHandle ppSessionInfo, out uint pCount)
    {

        winmdroot.Foundation.BOOL __result =
            PInvoke.WTSEnumerateSessions(hServer, Reserved, Version, out winmdroot.System.RemoteDesktop.WTS_SESSION_INFOW* ppSessionInfoLocal, out pCount);
        
        ppSessionInfo = new SafeWtsMemoryHandle((IntPtr)ppSessionInfoLocal, ownsHandle: true);
        
        return __result;
    }

    internal static string? WTSQuerySessionString(uint sessionId,
        winmdroot.System.RemoteDesktop.WTS_INFO_CLASS WTSInfoClass)
    {
        if (!PInvoke.WTSQuerySessionInformation(HANDLE.WTS_CURRENT_SERVER_HANDLE, sessionId, WTSInfoClass,
                out winmdroot.Foundation.PWSTR ppBuffer, out uint pBytesReturned))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Error while querying WTS session string");

        return ppBuffer.ToString();
    }
}