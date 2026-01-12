using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Text;
using Microsoft.PowerShell.Commands;
using Microsoft.Win32.SafeHandles;
using PSUserContext.Api.Extensions;
using PSUserContext.Cmdlets.Completers;
using PSUserContext.Cmdlets.Helpers;

namespace PSUserContext.Cmdlets;

[Cmdlet(VerbsLifecycle.Invoke, "UserContext", DefaultParameterSetName = "ById+ScriptBlock",
    SupportsShouldProcess = true)]
[OutputType(typeof(UserProcessResult))]
[OutputType(typeof(UserProcessWithOutputResult))]
public sealed class InvokeUserContextCommand : PSCmdlet
{
    // TODO: fix poor ParameterSet strategy
    private const string ById                 = "ById";
    private const string ByConsole            = "ByConsole";
    private const string CommandAct           = "+ScriptBlock";
    private const string PathAct              = "+Path";
    private const string LiteralPathAct       = "+LiteralPath";
    private const string ByIdCommand          = ById + CommandAct;
    private const string ByIdPath             = ById + PathAct;
    private const string ByIdPathLiteral      = ByIdPath + LiteralPathAct;
    private const string ByConsoleCommand     = ByConsole + CommandAct;
    private const string ByConsolePath        = ByConsole + PathAct;
    private const string ByConsolePathLiteral = ByConsole + LiteralPathAct;

    private const string RequiredPrivilege     = "SeDelegateSessionUserImpersonatePrivilege";
    private const string WindowsPowershellPath = @"C:\Windows\system32\WindowsPowerShell\v1.0\powershell.exe";

    private PropertyInfo? _preserveInvocationInfoOnce;
    private bool          _shouldExpandPath;
    private string        _path = string.Empty;

    private StringBuilder _sbCommand = new ($"\"{WindowsPowershellPath}\" -ExecutionPolicy Bypass -NoLogo -OutputFormat XML");

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
    public uint SessionId { get; set; }

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
    // this is broken due to messy parameter sets
    public string LiteralPath
    {
        get => _path;
        set => _path = value;
    }

    [Parameter(Position = 2)]
    [Alias("Args")]
    public string[] Arguments { get; set; } = Array.Empty<string>();

    // Attempting to specify -RedirectOutput together with -ShowWindow will result in a parameter binding error.
    // TODO: remove this parameter. output should be redirected by default unless -ShowWindow is specified
    [Parameter]
    [Alias("Out")]
    public SwitchParameter RedirectOutput { get; set; }

    [Parameter]
    [Alias("Visible")]
    public SwitchParameter ShowWindow { get; set; }

    protected override void BeginProcessing()
    {
        if (ShowWindow.IsPresent && RedirectOutput.IsPresent)
            throw new ParameterBindingException(
                "The parameters -ShowWindow and -RedirectOutput cannot be used together. This is a Win32 limitation.");

        // TODO: fix broken api
        // if (!TokenExtensions.HasTokenPrivilege(RequiredPrivilege))
        //     throw new InvalidOperationException(
        //         "Missing required privilege. You must run this script as SYSTEM or have the SeDelegateSessionUserImpersonatePrivilege token.");

        _preserveInvocationInfoOnce = typeof(ErrorRecord).GetProperty("PreserveInvocationInfoOnce",
            BindingFlags.NonPublic | BindingFlags.Instance);

        _sbCommand.AppendFormat(" -WindowStyle {0}", ShowWindow ? "Normal" : "Hidden");
    }
    
    // TODO: experiment with using NamedPipeConnectionInfo instead of parsing CLIXML output directly, similar to Enter-PSHostProcess
    // ref: https://github.com/PowerShell/PowerShell/blob/master/src/System.Management.Automation/engine/remoting/commands/EnterPSHostProcessCommand.cs
    protected override void ProcessRecord()
    {
        if (!ShouldProcess("ScriptBlock")) return;
        
        if (MyInvocation.BoundParameters.ContainsKey("ScriptBlock"))
        {
            string encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(ScriptBlock.ToString()));
            _sbCommand.Append($" -EncodedCommand {encodedCommand}");

            if (MyInvocation.BoundParameters.ContainsKey("Arguments"))
            {
                string arguments =
                    Convert.ToBase64String(
                        Encoding.Unicode.GetBytes(
                            PSSerializer.Serialize(Arguments))
                    );
                _sbCommand.Append($" -EncodedArguments {arguments}");
            }
        }
        else
        {

            FileInfo fileInfo;
            try
            {
                fileInfo = GetFileInfoFromPsPath(_path);
            }
            catch (Exception e)
            {
                throw new PSArgumentException($"Cannot invoke script file because {e.Message.ToLower()}", e);
            }
            // TODO: ensure target session can read & execute path
            _sbCommand.Append($" -File \"{fileInfo.FullName}\"");

            if (MyInvocation.BoundParameters.ContainsKey("Arguments"))
            {
                string arguments = string.Join(" ", Arguments);
                _sbCommand.Append($" {arguments}");
            }
        }

        SafeFileHandle primaryToken;

        if (Console.IsPresent)
        {
            var consoleId = SessionExtensions.GetActiveConsoleSession();

            if (consoleId is null)
            {
                throw new InvalidOperationException("No active console session found.");
            }

            primaryToken = TokenExtensions.GetSessionUserToken(consoleId, false);
        }
        else
        {
            primaryToken = TokenExtensions.GetSessionUserToken(SessionId, false);
        }

        if (primaryToken == null || primaryToken.IsInvalid)
            throw new InvalidOperationException("Failed to get a valid session user token.");
        
        using (primaryToken)
        {
            var redirectOptions = ShowWindow
                ? ProcessExtensions.RedirectFlags.None
                : ProcessExtensions.RedirectFlags.Output | ProcessExtensions.RedirectFlags.Error;

            var result = ProcessExtensions.CreateProcessAsUser(primaryToken,
                new ProcessExtensions.ProcessOptions
                {
                    ApplicationName = WindowsPowershellPath,
                    CommandLine = _sbCommand,
                    Redirect = redirectOptions,
                    WindowStyle = (ushort)(ShowWindow ? 5 : 0)
                });

            var output = CliXml.Deserialize(result.StdOutput);
            var err = CliXml.DeserializeError(result.StdError);

            if (output is not null)
                foreach (var o in output)
                    WriteObject(o);

            if (err is not null)
                foreach (var o in err)
                {
                    _preserveInvocationInfoOnce?.SetValue(o, true);
                    WriteError(o);
                }

            if (RedirectOutput.IsPresent)
                WriteObject(new UserProcessWithOutputResult
                {
                    ProcessId = result.ProcessId,
                    SessionId = SessionId,
                    ExitCode = result.ExitCode,
                    StandardOutput = result.StdOutput?.ToString() ?? string.Empty,
                    StandardError = result.StdError?.ToString() ?? string.Empty,
                });
            // else
            //     WriteObject(new UserProcessResult
            //     {
            //         ProcessId = result.ProcessId,
            //         SessionId = SessionId,
            //         ExitCode = result.ExitCode,
            //     });
        }
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
    public uint ExitCode  { get; set; }

    public override string ToString()
    {
        return $"PID {ProcessId} (Session {SessionId}): exit code {ExitCode}";
    }
}

public class UserProcessWithOutputResult : UserProcessResult
{
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError  { get; set; } = string.Empty;
}