using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Threading;
using Microsoft.Win32.SafeHandles;

namespace PSUserContext.Api.Extensions;

public static class ProcessExtensions
{
    public const uint INFINITE = uint.MaxValue;

    [Flags]
    public enum RedirectFlags
    {
        None = 0,
        Input,
        Output,
        Error
    }

    // TODO: refine options
    public sealed class ProcessOptions
    {
        public string?        ApplicationName;
        public StringBuilder? CommandLine;
        public string?        WorkingDirectory;
        public ushort         WindowStyle;
        public RedirectFlags  Redirect = RedirectFlags.None;
    }

    public sealed class UserProcessResult
    {
        public uint ProcessId { get; init; }

        public uint ExitCode { get; init; }

        public StringBuilder? StdOutput { get; init; }
        public StringBuilder? StdError  { get; init; }

        public UserProcessResult(uint           processId, uint exitCode, StringBuilder? stdOut = null,
                                 StringBuilder? stdErr = null)
        {
            ProcessId = processId;
            ExitCode = exitCode;
            StdOutput = stdOut;
            StdError = stdErr;
        }
    }

    private static Task<StringBuilder> ReadPipeTask(SafeFileHandle hRead, CancellationToken token = default)
    {
        return Task.Run(() =>
        {
            using var fs = new FileStream(hRead, FileAccess.Read, 4096, false);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            var sb = new StringBuilder();
            var buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
                sb.Append(Encoding.Default.GetString(buffer, 0, bytesRead));

            return sb;
        }, token);
    }

    public static unsafe ProcessJob CreateProcessAsUser(SafeFileHandle userToken, ProcessOptions options)
    {
        var securityAttribute = new SECURITY_ATTRIBUTES
        {
            nLength = (uint)Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
            lpSecurityDescriptor = null,
            bInheritHandle = true
        };

        // using NamedPipeServerStream pipeServer = new NamedPipeServerStream("psusercontext", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        
        // if (!PInvoke.CreatePipe(out var outRead, out var outWrite, securityAttribute, 4096))
        //     throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "failed to create output pipe");
        // if (!PInvoke.CreatePipe(out var errRead, out var errWrite, securityAttribute, 4096))
        //     throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "failed to create error pipe");
        // if (!PInvoke.SetHandleInformation(outRead, (uint)HANDLE_FLAGS.HANDLE_FLAG_INHERIT, 0))
        //     throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),
        //         "failed to set out read handle information");
        // if (!PInvoke.SetHandleInformation(errRead, (uint)HANDLE_FLAGS.HANDLE_FLAG_INHERIT, 0))
        //     throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),
        //         "failed to set err read handle information");
        // if (!PInvoke.SetHandleInformation(outWrite, (uint)HANDLE_FLAGS.HANDLE_FLAG_INHERIT, HANDLE_FLAGS.HANDLE_FLAG_INHERIT))
        //     throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),
        //         "failed to set out write handle info");
        // if (!PInvoke.SetHandleInformation(errWrite, (uint)HANDLE_FLAGS.HANDLE_FLAG_INHERIT, HANDLE_FLAGS.HANDLE_FLAG_INHERIT))
        //     throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),
        //         "failed to set err write handle info");

        PROCESS_INFORMATION processInfo;

        var startupInfo = new STARTUPINFOW
        {
            cb = (uint)Marshal.SizeOf<STARTUPINFOW>(),
            lpReserved = null,
            wShowWindow = options.WindowStyle,
            dwFlags = STARTUPINFOW_FLAGS.STARTF_USESHOWWINDOW // | STARTUPINFOW_FLAGS.STARTF_USESTDHANDLES,
            // hStdOutput = (HANDLE)pipeServer.SafePipeHandle.DangerousGetHandle(),
            // hStdError = (HANDLE)pipeServer.SafePipeHandle.DangerousGetHandle()
        };

        var dwCreationFlags = PROCESS_CREATION_FLAGS.CREATE_UNICODE_ENVIRONMENT;

        if (options.Redirect == RedirectFlags.None)
            dwCreationFlags |= PROCESS_CREATION_FLAGS.CREATE_NEW_CONSOLE;

        uint exitCode;

        if (!PInvoke.CreateSafeEnvironmentBlock(out var environment, userToken, false))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),
                "Failed to create environment block");

        using (environment)
        {
            var userProfilePath = environment.LatentGetVariable("USERPROFILE") ?? @"C:\Windows\System32";

            fixed (char* pDesktop = @"winsta0\default")
            {
                startupInfo.lpDesktop = new PWSTR(pDesktop);
                // TODO: refactor this line; i was really tired/lazy when I wrote it
                Span<char> commandLine = options.CommandLine is not null
                    ? new Span<char>(options.CommandLine.ToString().ToCharArray().Append('\0').ToArray())
                    : null;

                if (!PInvoke.CreateProcessAsUser(
                        userToken,
                        options.ApplicationName,
                        ref commandLine,
                        null,
                        null,
                        options.Redirect != 0,
                        dwCreationFlags,
                        environment.DangerousGetHandle().ToPointer(),
                        options.WorkingDirectory ?? userProfilePath,
                        startupInfo,
                        out processInfo
                    ))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),
                        "Failed to create process as user.");
            }
        }

        // var stdOutTask = ReadPipeTask(outRead);
        // var stdErrTask = ReadPipeTask(errRead);

        // outWrite?.Dispose();
        // errWrite?.Dispose();
        
        // Close thread handle; keep process handle for watcher
        if (!PInvoke.CloseHandle(processInfo.hThread))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Failed to close thread");
        
        var job = new ProcessJob(processInfo.dwProcessId, processInfo.hProcess);
        return job;

        // try
        // {
        //     PInvoke.WaitForSingleObject(processInfo.hProcess, INFINITE);
        //     PInvoke.GetExitCodeProcess(processInfo.hProcess, out exitCode);
        // }
        // finally
        // {
        //     PInvoke.CloseHandle(processInfo.hThread);
        //     PInvoke.CloseHandle(processInfo.hProcess);
        // }

        // if (options.Redirect == RedirectFlags.None)
        // return new UserProcessResult(processInfo.dwProcessId, exitCode);

        // return new UserProcessResult(processInfo.dwProcessId, exitCode, stdOutTask.GetAwaiter().GetResult(),
        //     stdErrTask.GetAwaiter().GetResult());
    }
    
}