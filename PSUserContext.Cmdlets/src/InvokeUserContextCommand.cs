using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.PowerShell.Commands;
using PSUserContext.Api.Extensions;
using PSUserContext.Cmdlets.Completers;

namespace PSUserContext.Cmdlets;

[Cmdlet(VerbsLifecycle.Invoke, "UserContext", DefaultParameterSetName = "ById+ScriptBlock",
    SupportsShouldProcess = true)]
[OutputType(typeof(UserProcessResult))]
[OutputType(typeof(UserProcessWithOutputResult))]
public sealed class InvokeUserContextCommand : PSCmdlet
{
    // TODO: fix poor ParameterSet strategy
    private const string ById = "ById";
    private const string ByConsole = "ByConsole";
    private const string CommandAct = "+ScriptBlock";
    private const string PathAct = "+Path";
    private const string LiteralPathAct = "+LiteralPath";
    private const string ByIdCommand = ById + CommandAct;
    private const string ByIdPath = ById + PathAct;
    private const string ByIdPathLiteral = ByIdPath + LiteralPathAct;
    private const string ByConsoleCommand = ByConsole + CommandAct;
    private const string ByConsolePath = ByConsole + PathAct;
    private const string ByConsolePathLiteral = ByConsole + LiteralPathAct;

    private const string RequiredPrivilege = "SeDelegateSessionUserImpersonatePrivilege";
    private const string WindowsPowershellPath = @"C:\Windows\system32\WindowsPowerShell\v1.0\powershell.exe";

    private PropertyInfo? _preserveInvocationInfoOnce;
    private bool _shouldExpandPath;
    private string _path = string.Empty;
    private uint _sessionId = UInt32.MaxValue;

    // TODO: append -NonInteractive if the current host does not support interactivity.
    // https://github.com/PowerShell/PowerShell/blob/master/src/Microsoft.PowerShell.ConsoleHost/host/msh/ConsoleHost.cs
    private StringBuilder _sbCommand =
        new($"\"{WindowsPowershellPath}\" -ExecutionPolicy RemoteSigned -NoLogo -NoProfile -WindowStyle Hidden -NamedPipeServerMode");

    /// <summary>
    /// The session ID of the user context to invoke.
    /// </summary>
    /// <remarks>
    /// Session ID can be located with the native <c>quser</c> command and the <c>Get-UserContext</c> cmdlet.
    /// </remarks>
    [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true,
        ParameterSetName = ByIdCommand)]
    [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true,
        ParameterSetName = ByIdPath)]
    [ValidateNotNullOrEmpty]
    public uint SessionId
    {
        get => _sessionId;
        set => _sessionId = value;
    }

    /// <summary>
    /// When specified, invokes the active console session.
    /// </summary>
    /// <remark>
    /// If no active console session is found, the Cmdlet throws an <see cref="InvalidOperationException">InvalidOperationException</see>
    /// </remark>
    [Parameter(Mandatory = true, ParameterSetName = ByConsoleCommand)]
    [Parameter(Mandatory = true, ParameterSetName = ByConsolePath)]
    public SwitchParameter Console { get; set; }

    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ByIdCommand)]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ByConsoleCommand)]
    public ScriptBlock ScriptBlock { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = ByIdPath)]
    [Parameter(Mandatory = true, ParameterSetName = ByConsolePath)]
    [ArgumentCompleter(typeof(ScriptFileCompleter))]
    [ValidateNotNullOrEmpty]
    // TODO: add file support for new runspace method
    // currently broken
    public string Path
    {
        get => _path;
        set
        {
            _shouldExpandPath = true;
            _path = value;
        }
    }

    [Parameter(Mandatory = true, ParameterSetName = ByIdPathLiteral)]
    [Parameter(Mandatory = true, ParameterSetName = ByConsolePathLiteral)]
    [ArgumentCompleter(typeof(ScriptFileCompleter))]
    [ValidateNotNullOrEmpty]
    // currently broken
    public string LiteralPath
    {
        get => _path;
        set => _path = value;
    }

    [Parameter(Position = 2)]
    [Alias("Args")]
    public string[] Arguments { get; set; } = Array.Empty<string>();

    protected override void BeginProcessing()
    {
        if (!TokenExtensions.HasTokenPrivilege(RequiredPrivilege))
            throw new InvalidOperationException(
                "Missing required privilege. You must run this script as SYSTEM or have the SeDelegateSessionUserImpersonatePrivilege token.");

        if (Console.IsPresent)
        {
            WriteVerbose("Using active console session.");
            uint? consoleId = SessionExtensions.GetConsoleSessionId();

            if (consoleId is null)
            {
                throw new ItemNotFoundException("No active console session found.");
            }

            _sessionId = consoleId.Value;
        }

        _preserveInvocationInfoOnce = typeof(ErrorRecord).GetProperty("PreserveInvocationInfoOnce",
            BindingFlags.NonPublic | BindingFlags.Instance);
    }

    protected override void ProcessRecord()
    {
        // check if sessionId is set. 0 is a safe default as this Cmdlet is not intended to invoke system space contexts
        if (_sessionId == 0)
            throw new PSArgumentException("Session ID 0 is reserved for system services; specify an interactive session ID instead.");
        
        if (_sessionId == UInt32.MaxValue)
            throw new PSArgumentException("Session ID is not valid.");
        
        // TODO: properly support ShouldProcess
        if (!ShouldProcess($"session {_sessionId}", "creating powershell process")) return;

        using var result = ProcessExtensions.CreateProcessAsUser(_sessionId,
            new ProcessExtensions.ProcessOptions
            {
                ApplicationName = WindowsPowershellPath,
                CommandLine = _sbCommand,
                Redirect = ProcessExtensions.RedirectFlags.None,
                WindowStyle = 0
            });

        NamedPipeConnectionInfo connectionInfo = new NamedPipeConnectionInfo(Convert.ToInt32(result.Pid));

        try
        {
            TypeTable typeTable = TypeTable.LoadDefaultTypeFiles();
            using Runspace runspace = RunspaceFactory.CreateRunspace(connectionInfo, this.Host, typeTable);
            Runspace.DefaultRunspace.Debugger.SetDebugMode(DebugModes.None);
            using var ps = PowerShell.Create();

            ps.Streams.Error.DataAdded += (sender, args) =>
            {
                var errors = (PSDataCollection<ErrorRecord>)sender;
                var e = errors[args.Index];

                if (e is { Exception: RemoteException re })
                {
                    e = re.ErrorRecord;
                    _preserveInvocationInfoOnce?.SetValue(e, true);
                }

                EnqueueStream(() => WriteError(e));
            };

            var output = new PSDataCollection<PSObject>();

            output.DataAdded += (sender, args) =>
            {
                var outputs = (PSDataCollection<PSObject>)sender;
                var o = outputs[args.Index];

                EnqueueStream(() => WriteObject(o));
            };

            ps.Runspace = runspace;
            ps.Runspace.Open();
            ps.AddScript(ScriptBlock.ToString()).Invoke(input: null, output: output);

            DrainStream();
            ps.Runspace.Close();
        }
        catch (Exception e)
        {
            WriteError(new ErrorRecord(e, e.Message, ErrorCategory.InvalidOperation, this));
        }
    }

    private long _seq;
    private readonly ConcurrentQueue<(long seq, Action emit)> _queue = new();

    private void EnqueueStream(Action emit)
    {
        var s = Interlocked.Increment(ref _seq);
        _queue.Enqueue((s, emit));
    }

    private void DrainStream()
    {
        while (_queue.TryDequeue(out var item))
            item.emit();
    }

    private FileInfo GetFileInfoFromPsPath(string psPath)
    {
        ProviderInfo provider;

        List<string> filePaths = new List<string>();

        try
        {
            if (_shouldExpandPath)
                filePaths.AddRange(this.GetResolvedProviderPathFromPSPath(psPath, out provider));
            else
                filePaths.Add(this.SessionState.Path.GetUnresolvedProviderPathFromPSPath(psPath, out provider, out _));
        }
        catch (ItemNotFoundException e)
        {
            throw new InvalidOperationException($"Path '{psPath}' does not exist.", e);
        }

        switch (filePaths.Count)
        {
            case 0:
                throw new InvalidOperationException($"Path `{psPath}` does not exist.");
            case > 1:
                throw new InvalidOperationException($"Path '{psPath}' expanded to multiple files.");
        }

        if (!IsFileSystemPath(provider, filePaths.First()))
            throw new InvalidOperationException($"Path '{filePaths.First()}' is not a FileSystem path.");

        if (File.Exists(filePaths.First()))
        {
            return new FileInfo(filePaths.First());
        }

        // This could be a permission issue
        throw new InvalidOperationException("An unexpected error occurred");
    }

    private bool IsFileSystemPath(ProviderInfo provider, string path)
    {
        bool isFileSystem = true;

        if (provider.ImplementingType != typeof(FileSystemProvider))
        {
            // create a .NET exception wrapping our error text
            ArgumentException ex = new ArgumentException(path +
                                                         " does not resolve to a path on the FileSystem provider.");
            // wrap this in a powershell errorrecord
            ErrorRecord error = new ErrorRecord(ex, "InvalidProvider",
                ErrorCategory.InvalidArgument, path);
            // write a non-terminating error to pipeline
            WriteError(error);
            // tell our caller that the item was not on the filesystem
            isFileSystem = false;
        }

        return isFileSystem;
    }
}

public class UserProcessResult
{
    public uint ProcessId { get; set; }
    public uint SessionId { get; set; }
    public uint ExitCode { get; set; }

    public override string ToString()
    {
        return $"PID {ProcessId} (Session {SessionId}): exit code {ExitCode}";
    }
}

public class UserProcessWithOutputResult : UserProcessResult
{
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
}