using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.PowerShell.Commands;
using PSUserContext.Api.Extensions;
using PSUserContext.Cmdlets.Completers;

namespace PSUserContext.Cmdlets;

[Cmdlet(VerbsLifecycle.Invoke, "UserContext", DefaultParameterSetName = "ByIdUsingScriptBlock",
    SupportsShouldProcess = true)]
[OutputType(typeof(UserProcessResult))]
[OutputType(typeof(UserProcessWithOutputResult))]
public sealed class InvokeUserContextCommand : PSCmdlet
{
    // TODO: fix poor ParameterSet strategy
    private const string ById                                      = "ById";
    private const string ByConsole                                 = "ByConsole";
    private const string UsingScriptBlock                          = "UsingScriptBlock";
    private const string UsingPath                                 = "UsingPath";
    private const string UsingLiteralPath                          = "UsingLiteralPath";
    private const string WithArgumentList                          = "WithArgumentList";
    private const string WithParameters                            = "WithParameters";
    private const string ByIdUsingScriptBlock      = ById + UsingScriptBlock;
    private const string ByIdUsingPath             = ById + UsingPath;
    private const string ByIdUsingLiteralPath      = ById + UsingLiteralPath;
    private const string ByConsoleUsingScriptBlock = ByConsole + UsingScriptBlock;
    private const string ByConsoleUsingPath        = ByConsole + UsingPath;
    private const string ByConsoleUsingLiteralPath = ByConsole + UsingLiteralPath;

    private const string RequiredPrivilege = "SeDelegateSessionUserImpersonatePrivilege";
    private const string WindowsPowershellPath = @"C:\Windows\system32\WindowsPowerShell\v1.0\powershell.exe";
    
    private readonly ConcurrentQueue<(long seq, Action emit)> _queue = new();

    private PropertyInfo? _preserveInvocationInfoOnce;

    // TODO: append -NonInteractive if the current host does not support interactivity.
    // https://github.com/PowerShell/PowerShell/blob/master/src/Microsoft.PowerShell.ConsoleHost/host/msh/ConsoleHost.cs
    private readonly StringBuilder _sbCommand =
        new(
            $"\"{WindowsPowershellPath}\" -ExecutionPolicy RemoteSigned -NoLogo -NoProfile -WindowStyle Hidden -NamedPipeServerMode");

    private long   _seq;
    
    private string       _script = string.Empty;
    private ScriptBlock? _scriptBlock;
    private bool         _shouldExpandPath;
    private string       _filePath = string.Empty;

    /// <summary>
    ///     The session ID of the user context to invoke.
    /// </summary>
    /// <remarks>
    ///     Session ID can be located with the native <c>quser</c> command and the <c>Get-UserContext</c> cmdlet.
    /// </remarks>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true,
        ParameterSetName = ByIdUsingScriptBlock)]
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true,
        ParameterSetName = ByIdUsingPath)]
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true,
        ParameterSetName = ByIdUsingLiteralPath)]
    [ValidateNotNullOrEmpty]
    public uint SessionId { get; set; } = uint.MaxValue;

    /// <summary>
    ///     When specified, invokes the active console session.
    /// </summary>
    /// <remark>
    ///     If no active console session is found, the Cmdlet throws an
    ///     <see cref="InvalidOperationException">InvalidOperationException</see>
    /// </remark>
    [Parameter(Mandatory = true, ParameterSetName = ByConsoleUsingScriptBlock)]
    [Parameter(Mandatory = true, ParameterSetName = ByConsoleUsingPath)]
    [Parameter(Mandatory = true, ParameterSetName = ByConsoleUsingLiteralPath)]
    public SwitchParameter Console { get; set; }

    [Parameter(Mandatory = true, Position = 1, ParameterSetName = ByIdUsingScriptBlock)]
    [Parameter(Mandatory = true, Position = 1, ParameterSetName = ByConsoleUsingScriptBlock)]
    public ScriptBlock ScriptBlock
    {
        get => _scriptBlock;
        set
        {
            _script = value.ToString();
            _scriptBlock = value;
        }
    }

    [Parameter(Mandatory = true, Position = 1, ParameterSetName = ByIdUsingPath)]
    [Parameter(Mandatory = true, Position = 1, ParameterSetName = ByConsoleUsingPath)]
    [ArgumentCompleter(typeof(ScriptFileCompleter))]
    [ValidateNotNullOrEmpty]
    public string Path
    {
        get => _filePath;
        set
        {
            _shouldExpandPath = true;
            _filePath = value;
        }
    }

    [Parameter(Mandatory = true, Position = 1, ParameterSetName = ByIdUsingLiteralPath)]
    [Parameter(Mandatory = true, Position = 1, ParameterSetName = ByConsoleUsingLiteralPath)]
    [ArgumentCompleter(typeof(ScriptFileCompleter))]
    [ValidateNotNullOrEmpty]
    public string LiteralPath
    {
        get => _filePath;
        set
        {
            _shouldExpandPath = false;
            _filePath = value;
        }
    }

    /// <summary>
    ///     Supplies the values of parameters for the scriptblock or file by position. The parameters in the scriptblock are
    ///     passed by position from the array value supplied to ArgumentList. This is known as array splatting.
    /// </summary>
    [Parameter(Position = 2)]
    public object[] ArgumentList { get; set; } = [];

    protected override void BeginProcessing()
    {
        if (!TokenExtensions.HasTokenPrivilege(RequiredPrivilege))
            throw new InvalidOperationException(
                "Missing required privilege. You must run this script as SYSTEM or have the SeDelegateSessionUserImpersonatePrivilege token.");

        if (_filePath != string.Empty)
        {
            // Read contents of file and put in _script
            var file = GetFileInfoFromPsPath(_filePath, _shouldExpandPath);

            if (file.Exists) _script = File.ReadAllText(file.FullName, Encoding.UTF8);
        }

        if (Console.IsPresent)
        {
            WriteVerbose("Using active console session.");
            uint? consoleId = SessionExtensions.GetConsoleSessionId();

            if (consoleId is null) throw new ItemNotFoundException("No active console session found.");

            SessionId = consoleId.Value;
        }

        _preserveInvocationInfoOnce = typeof(ErrorRecord).GetProperty("PreserveInvocationInfoOnce",
            BindingFlags.NonPublic | BindingFlags.Instance);
    }

    protected override void ProcessRecord()
    {
        // check if sessionId is set. 0 is a safe default as this Cmdlet is not intended to invoke system space contexts
        if (SessionId == 0)
            throw new PSArgumentException(
                "Session ID 0 is reserved for system services; specify an interactive session ID instead.");

        if (SessionId == uint.MaxValue)
            throw new PSArgumentException("Session ID is not valid.");

        // TODO: properly support ShouldProcess
        if (!ShouldProcess($"session {SessionId}",
                $"executing {(MyInvocation.BoundParameters.ContainsKey("ScriptBlock") ? "scriptblock" : "file")}"))
            return;

        using var result = ProcessExtensions.CreateProcessAsUser(SessionId,
            new ProcessExtensions.ProcessOptions
            {
                ApplicationName = WindowsPowershellPath,
                CommandLine = _sbCommand,
                Redirect = ProcessExtensions.RedirectFlags.None,
                WindowStyle = 0
            });

        var connectionInfo = new NamedPipeConnectionInfo(Convert.ToInt32(result.Pid));

        try
        {
            var typeTable = TypeTable.LoadDefaultTypeFiles();
            using var runspace = RunspaceFactory.CreateRunspace(connectionInfo, Host, typeTable);
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

            ps.AddScript(_script);

            if (ArgumentList.Length > 0)
            {
                foreach (object arg in ArgumentList) ps.AddArgument(arg);
            }

            ps.Invoke(null, output);

            DrainStream();
            ps.Runspace.Close();
        }
        catch (Exception e)
        {
            WriteError(new ErrorRecord(e, e.Message, ErrorCategory.InvalidOperation, this));
        }
    }

    private void EnqueueStream(Action emit)
    {
        long s = Interlocked.Increment(ref _seq);
        _queue.Enqueue((s, emit));
    }

    private void DrainStream()
    {
        while (_queue.TryDequeue(out var item))
            item.emit();
    }

    private FileInfo GetFileInfoFromPsPath(string psPath, bool expandPath = false)
    {
        ProviderInfo provider;
        List<string> filePaths = new();

        try
        {
            if (expandPath)
                filePaths.AddRange(SessionState.Path.GetResolvedProviderPathFromPSPath(psPath, out provider));
            else
                filePaths.Add(SessionState.Path.GetUnresolvedProviderPathFromPSPath(psPath, out provider, out _));
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

        string filePath = filePaths.First();

        if (!IsFileSystemPath(provider, filePath))
            throw new InvalidOperationException($"Path '{filePath}' is not a FileSystem path.");

        if (File.Exists(filePath))
        {
            // TODO: Add support for path expansion returning multiple files?
            return new FileInfo(filePath);
        }

        // This could be a permission issue
        throw new ItemNotFoundException($"The path '{filePath}' does not exist or is inaccessible.");
    }

    private bool IsFileSystemPath(ProviderInfo provider, string path)
    {
        var isFileSystem = true;

        if (provider.ImplementingType != typeof(FileSystemProvider))
        {
            // create a .NET exception wrapping our error text
            var ex = new ArgumentException(path +
                                           " does not resolve to a path on the FileSystem provider.");
            // wrap this in a powershell errorrecord
            var error = new ErrorRecord(ex, "InvalidProvider",
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