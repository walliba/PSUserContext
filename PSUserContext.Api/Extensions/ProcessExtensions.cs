using PSUserContext.Api.Helpers;
using PSUserContext.Api.Interop;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace PSUserContext.Api.Extensions;

public static class ProcessExtensions
{
    /// <summary>
    /// Flags for handle inheritance and protection
    /// Corresponds to Win32 HANDLE_FLAGS_* constants
    /// </summary>
    [Flags]
    public enum HandleFlags : uint
    {
        None             = 0x00000000,
        Inherit          = 0x00000001, // HANDLE_FLAG_INHERIT
        ProtectFromClose = 0x00000002  // HANDLE_FLAG_PROTECT_FROM_CLOSE
    }

    /// <summary>
    /// Flags for STARTUPINFO structure.
    /// Corresponds to Win32 STARTF_* constants
    /// </summary>
    [Flags]
    public enum StartupInfoFlags : uint
    {
        None             = 0x00000000,
        UseShowWindow    = 0x00000001, // STARTF_USESHOWWINDOW
        UseSize          = 0x00000002, // STARTF_USESIZE
        UsePosition      = 0x00000004, // STARTF_USEPOSITION
        UseCountChars    = 0x00000008, // STARTF_USECOUNTCHARS
        UseFillAttribute = 0x00000010, // STARTF_USEFILLATTRIBUTE
        RunFullScreen    = 0x00000020, // STARTF_RUNFULLSCREEN
        ForceOnFeedback  = 0x00000040, // STARTF_FORCEONFEEDBACK
        ForceOffFeedback = 0x00000080, // STARTF_FORCEOFFFEEDBACK
        UseStdHandles    = 0x00000100, // STARTF_USESTDHANDLES
        UseHotKey        = 0x00000200, // STARTF_USEHOTKEY
        TitleIsLinkName  = 0x00000800, // STARTF_TITLEISLINKNAME
        TitleIsAppId     = 0x00001000, // STARTF_TITLEISAPPID
        PreventPinning   = 0x00002000, // STARTF_PREVENTPINNING
        UntrustedSource  = 0x00008000  // STARTF_UNTRUSTEDSOURCE
    }

    /// <summary>
    /// Flags controlling process creation behavior.
    /// Corresponds to CREATE_* constants.
    /// </summary>
    [Flags]
    public enum ProcessCreationFlags : uint
    {
        None                         = 0x00000000,
        DebugProcess                 = 0x00000001, // DEBUG_PROCESS
        DebugOnlyThisProcess         = 0x00000002, // DEBUG_ONLY_THIS_PROCESS
        CreateSuspended              = 0x00000004, // CREATE_SUSPENDED
        CreateNewConsole             = 0x00000010, // CREATE_NEW_CONSOLE
        CreateNewProcessGroup        = 0x00000200, // CREATE_NEW_PROCESS_GROUP
        CreateUnicodeEnvironment     = 0x00000400, // CREATE_UNICODE_ENVIRONMENT
        CreateSeparateWowVdm         = 0x00000800, // CREATE_SEPARATE_WOW_VDM
        CreateSharedWowVdm           = 0x00001000, // CREATE_SHARED_WOW_VDM
        CreateProtectedProcess       = 0x00040000, // CREATE_PROTECTED_PROCESS
        CreateBreakawayFromJob       = 0x01000000, // CREATE_BREAKAWAY_FROM_JOB
        CreatePreserveCodeAuthzLevel = 0x02000000, // CREATE_PRESERVE_CODE_AUTHZ_LEVEL
        CreateDefaultErrorMode       = 0x04000000, // CREATE_DEFAULT_ERROR_MODE
        CreateNoWindow               = 0x08000000  // CREATE_NO_WINDOW
    }

    public const uint INFINITE = uint.MaxValue;

    [Flags]
    public enum RedirectFlags
    {
        None = 0,
        Input,
        Output,
        Error
    }

    // todo: refine options
    public sealed class ProcessOptions
    {
        public string?         ApplicationName;
        public StringBuilder?  CommandLine;
        public string?         WorkingDirectory;
        public InteropTypes.SW WindowStyle;
        public RedirectFlags   Redirect = RedirectFlags.None;
    }

    public sealed class UserProcessResult
    {
        public uint ProcessId { get; init; }
        
        public uint ExitCode { get; init; }
        
        public string StdOutput { get; init; }
        public string StdError { get; init; }

        public UserProcessResult(uint processId, uint exitCode, string? stdOut = null, string? stdErr = null)
        {
            ProcessId = processId;
            ExitCode = exitCode;
            StdOutput = stdOut ?? string.Empty;
            StdError = stdErr ??  string.Empty;
        }
    }

    private static Task<string> ReadPipeTask(SafeFileHandle hRead, CancellationToken token = default)
    {
        return Task.Run(() =>
        {
            using var fs = new FileStream(hRead, FileAccess.Read, 4096, false);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            var sb = new StringBuilder();
            byte[] buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
            {
                sb.Append(Encoding.Default.GetString(buffer, 0, bytesRead));
            }

            return sb.ToString();
        }, token);
    }

    public static UserProcessResult CreateProcessAsUser(SafeNativeHandle userToken, ProcessOptions options)
    {
        var securityAttribute = new InteropTypes.SECURITY_ATTRIBUTES
        {
            nLength = (uint)Marshal.SizeOf<InteropTypes.SECURITY_ATTRIBUTES>(),
            bInheritHandle = true,
            lpSecurityDescriptor = IntPtr.Zero
        };
    
        if (!Kernel32.CreatePipe(out var outRead, out var outWrite, ref securityAttribute, 4096)) // OS Default: 4 KB
            throw new InvalidOperationException("failed to create output pipe");
        if (!Kernel32.CreatePipe(out var errRead, out var errWrite, ref securityAttribute, 4096)) // OS Default: 4 KB
            throw new InvalidOperationException("failed to create error pipe");
        if (!Kernel32.SetHandleInformation(outRead, (uint)HandleFlags.Inherit, 0))
            throw new InvalidOperationException("failed to set out read handle info");
        if (!Kernel32.SetHandleInformation(errRead, (uint)HandleFlags.Inherit, 0))
            throw new InvalidOperationException("failed to set err read handle info");
        if (!Kernel32.SetHandleInformation(outWrite, (uint)HandleFlags.Inherit, (uint)HandleFlags.Inherit))
            throw new InvalidOperationException("failed to set out write handle info");
        if (!Kernel32.SetHandleInformation(errWrite, (uint)HandleFlags.Inherit, (uint)HandleFlags.Inherit))
            throw new InvalidOperationException("failed to set err write handle info");

        InteropTypes.PROCESS_INFORMATION processInfo;
        var startupInfo = new InteropTypes.STARTUPINFO
        {
            cb = (uint)Marshal.SizeOf<InteropTypes.STARTUPINFO>(),
            lpDesktop = @"winsta0\default",
            wShowWindow = options.WindowStyle,
            dwFlags = StartupInfoFlags.UseStdHandles | StartupInfoFlags.UseShowWindow,
            hStdOutput = outWrite,
            hStdError = errWrite
        };

        var dwCreationFlags = ProcessCreationFlags.CreateUnicodeEnvironment;

        if (options.Redirect == RedirectFlags.None)
            dwCreationFlags |= ProcessCreationFlags.CreateNewConsole;
        
        uint exitCode;
        
        using (var environment = EnvExtensions.CreateEnvironmentBlock(userToken))
        {
            string? userProfilePath = environment.LatentGetVariable("USERPROFILE") ?? @"C:\Windows\System32";
            
            if (!Advapi32.CreateProcessAsUserW(
                    userToken,
                    options.ApplicationName,
                    options.CommandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    options.Redirect != 0,
                    (uint)dwCreationFlags,
                    environment,
                    options.WorkingDirectory ?? userProfilePath,
                    ref startupInfo,
                    out processInfo))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcessAsUser failed");
        }
        
        var stdOutTask = ReadPipeTask(outRead);
        var stdErrTask = ReadPipeTask(errRead);

        outWrite?.Dispose();
        errWrite?.Dispose();

        try
        {
            Kernel32.WaitForSingleObject(processInfo.hProcess, INFINITE);
            Kernel32.GetExitCodeProcess(processInfo.hProcess, out exitCode);
        }
        finally
        {
            Kernel32.CloseHandle(processInfo.hThread);
            Kernel32.CloseHandle(processInfo.hProcess);
        }
        
        if (options.Redirect == RedirectFlags.None)
            return new UserProcessResult(processInfo.dwProcessId, exitCode);

        return new UserProcessResult(processInfo.dwProcessId, exitCode, stdOutTask.GetAwaiter().GetResult(), stdErrTask.GetAwaiter().GetResult());
    }
}