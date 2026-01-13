using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.Foundation;
using Microsoft.Win32.SafeHandles;

namespace PSUserContext.Api;

// IAsyncDisposable does not exist in target .NET
public sealed class ProcessJob : IDisposable
{
    public uint Pid { get; }
    public event Action<uint>? ExitCode;

    // TODO: support SafeHandle
    private readonly HANDLE       _hProcess;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task                    _watchTask;

    internal ProcessJob(uint pid, in HANDLE hProcess)
    {
        Pid = pid;
        _hProcess = hProcess;

        _watchTask = WatchAsync(_cts.Token);
    }

    private async Task WatchAsync(CancellationToken ct)
    {
        await Task.Run(() =>
        {
            PInvoke.WaitForSingleObject(_hProcess, uint.MaxValue);
            PInvoke.GetExitCodeProcess(_hProcess, out var exitCode);
            ExitCode?.Invoke(exitCode);
        }, ct).ConfigureAwait(false);
    }
    
    public void Dispose()
    {
        _cts.Cancel();

        try
        {
            // wait for program to exit
            _watchTask.Wait(250);
        }
        catch (Exception)
        {
            // ignore
        }
        
        if (!PInvoke.TerminateProcess(_hProcess, 0))
            Console.WriteLine("Failed to terminate process handle");
        
        if (!PInvoke.CloseHandle(_hProcess))
            Console.WriteLine("Failed to close process handle");

        _cts.Dispose();
    }
}