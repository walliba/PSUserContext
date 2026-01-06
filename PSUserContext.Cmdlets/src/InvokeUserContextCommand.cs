using PSUserContext.Api.Interop;
using System;
using System.IO;
using System.IO.Pipes;
using System.Management.Automation;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using PSUserContext.Api.Extensions;
using PSUserContext.Cmdlets.Completers;
using PSUserContext.Cmdlets.Helpers;

namespace PSUserContext.Cmdlets;

[Cmdlet(VerbsLifecycle.Invoke, "UserContext", DefaultParameterSetName = "ById+ScriptBlock", SupportsShouldProcess = true)]
[OutputType(typeof(UserProcessResult))]
[OutputType(typeof(UserProcessWithOutputResult))]
public sealed class InvokeUserContextCommand : PSCmdlet
{
    private const string ById        = "ById";
    private const string ByConsole   = "ByConsole";
    private const string CommandAct  = "+ScriptBlock";
    private const string FileAct     = "+File";
    private const string RedirectAct = "+Redirect";
    private const string VisibleAct  = "+Visible";
    
    private const string ByIdCommand   = ById + CommandAct;
    private const string ByIdFile      = ById + FileAct;
    
    private const string ByConsoleCommand = ByConsole + CommandAct;
    private const string ByConsoleFile = ByConsole + FileAct;

    [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 0, ParameterSetName = ByIdCommand)]
    [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 0, ParameterSetName = ByIdFile)]
    public uint SessionId { get; set; } = InteropTypes.INVALID_SESSION_ID;
    
    [Parameter(Mandatory = true, ParameterSetName = ByIdCommand)]
    [Parameter(Mandatory = true, ParameterSetName = ByConsoleCommand)]
    public ScriptBlock? ScriptBlock { get; set; }
    
    [Parameter(Mandatory = true, ParameterSetName = ByIdFile)]
    [Parameter(Mandatory = true, ParameterSetName = ByConsoleFile)]
    [Alias("File")]
    [ArgumentCompleter(typeof(ScriptFileCompleter))]
    public FileInfo FilePath { get; set; }
    
    [Parameter(Position = 2)]
    [Alias("Args")]
    public string[] Arguments { get; set; } = Array.Empty<string>();
    
    // Attempting to specify -RedirectOutput together with -ShowWindow will result in a parameter binding error.
    [Parameter] 
    [Alias("Out")] 
    public SwitchParameter RedirectOutput { get; set; }
    
    // todo: implement PassThru parameter
    
    [Parameter]
    [Alias("Visible")]
    public SwitchParameter ShowWindow { get; set; }
    
    /// <summary>
    /// When specified, invokes the active console session.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = ByConsoleCommand)]
    [Parameter(Mandatory = true, ParameterSetName = ByConsoleFile)]
    public SwitchParameter Console { get; set; }

    private const string RequiredPrivilege = "SeDelegateSessionUserImpersonatePrivilege";
    private const string PowerShellPath    = @"C:\Windows\system32\WindowsPowerShell\v1.0\powershell.exe";
    
    private PropertyInfo? _propertyInfo;
    
    protected override void BeginProcessing()
    {   
        // temporary solution until I source generate parameter sets
        if (ShowWindow.IsPresent && RedirectOutput.IsPresent)
            throw new ParameterBindingException(
                "The parameters -ShowWindow and -RedirectOutput cannot be used together. This is a Win32 limitation.");
        
        if (!TokenExtensions.HasTokenPrivilege(RequiredPrivilege))
            throw new InvalidOperationException(
                "Missing required privilege. You must run this script as SYSTEM or have the SeDelegateSessionUserImpersonatePrivilege token.");
        
        _propertyInfo = typeof(ErrorRecord).GetProperty("PreserveInvocationInfoOnce", BindingFlags.NonPublic | BindingFlags.Instance);
    }
    protected override void ProcessRecord()
    {
        if (!ShouldProcess("ScriptBlock")) return;

        StringBuilder sbCommand =
            new StringBuilder(
                $"\"{PowerShellPath}\" -ExecutionPolicy Bypass -NoLogo -OutputFormat XML -WindowStyle {(ShowWindow ? "Normal" : "Hidden")}");
        
        if (ParameterSetName.Equals(ByIdFile))
        {
            // Should probably copy to session's temp, ensuring the user has access to the file.
            sbCommand.Append($" -File \"{FilePath.FullName}\"");
            
            if (MyInvocation.BoundParameters.ContainsKey("Arguments"))
            {
                string arguments = string.Join(" ", Arguments);
                sbCommand.Append($" {arguments}");
            }
        }
        else
        {
            string encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(ScriptBlock!.ToString()));
            sbCommand.Append($" -EncodedCommand {encodedCommand}");
            
            if (MyInvocation.BoundParameters.ContainsKey("Arguments"))
            {
                string arguments =
                    Convert.ToBase64String(
                        Encoding.Unicode.GetBytes(
                            PSSerializer.Serialize(Arguments))
                            );
                sbCommand.Append($" -EncodedArguments {arguments}");
            }
        }

        SafeAccessTokenHandle primaryToken;
        
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

            var ipcPath =
                "D:\\source\\UserContext\\PSUserContext.UserHost\\bin\\Release\\net472\\PSUserContext.UserHost.exe";

            await ProcessExtensions.CreateProcessAsUserAsync(primaryToken,
                new ProcessExtensions.ProcessOptions
                {
                    // ApplicationName = PowerShellPath,
                    // CommandLine = sbCommand,
                    ApplicationName = ipcPath,
                    CommandLine = null,
                    Redirect = redirectOptions,
                    WindowStyle = ShowWindow ? InteropTypes.SW.SHOW : InteropTypes.SW.HIDE
                });
            
            // var output = CliXml.Deserialize(result.StdOutput);
            // var err = CliXml.DeserializeError(result.StdError);
            //
            // if (output is not null)
            //     foreach (var o in output)
            //         WriteObject(o);
            //
            // if (err is not null)
            //     foreach (var o in err)
            //     {
            //         _propertyInfo?.SetValue(o, true);
            //         WriteError(o);
            //     }
            //
            // if (RedirectOutput.IsPresent)
            //     WriteObject(new UserProcessWithOutputResult
            //     {
            //         ProcessId = result.ProcessId,
            //         SessionId = SessionId,
            //         ExitCode = result.ExitCode,
            //         StandardOutput = result.StdOutput?.ToString() ?? string.Empty,
            //         StandardError = result.StdError?.ToString() ?? string.Empty,
            //     });
            // else
            //     WriteObject(new UserProcessResult
            //     {
            //         ProcessId = result.ProcessId,
            //         SessionId = SessionId,
            //         ExitCode = result.ExitCode,
            //     });
            
        }
        
        using (NamedPipeClientStream pipeClient =
               new NamedPipeClientStream(".", "testpipe", PipeDirection.In))
        {

            // Connect to the pipe or wait until the pipe is available.
            WriteWarning("Attempting to connect to pipe...");
            pipeClient.Connect();

            WriteWarning("Connected to pipe.");
            WriteWarning($"There are currently {pipeClient.NumberOfServerInstances} pipe server instances open.");
            using (StreamReader sr = new StreamReader(pipeClient))
            {
                // Display the read text to the console
                string temp;
                while ((temp = sr.ReadLine()) != null)
                {
                    WriteWarning($"Received from server: {temp}");
                }
            }
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            var result = await ProcessExtensions.CreateProcessAsUserAsync(primaryToken,
                new ProcessExtensions.ProcessOptions
                {
                    // ApplicationName = PowerShellPath,
                    // CommandLine = sbCommand,
                    ApplicationName = ipcPath,
                    CommandLine = null,
                    Redirect = redirectOptions,
                    WindowStyle = ShowWindow ? InteropTypes.SW.SHOW : InteropTypes.SW.HIDE
                });
        }
    }
}

public class UserProcessResult
{
    public uint   ProcessId   { get; set; }
    public uint   SessionId   { get; set; }
    public uint    ExitCode    { get; set; }

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