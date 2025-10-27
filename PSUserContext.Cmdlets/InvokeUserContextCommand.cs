using PSUserContext.Api.Interop;
using System;
using System.IO;
using System.Management.Automation;
using System.Text;
using PSUserContext.Api.Extensions;
using PSUserContext.Cmdlets.Completers;

namespace PSUserContext.Cmdlets;

[Cmdlet(VerbsLifecycle.Invoke, "UserContext", DefaultParameterSetName = "ById+Command", SupportsShouldProcess = true)]
[OutputType(typeof(UserProcessResult))]
[OutputType(typeof(UserProcessWithOutputResult))]
public sealed class InvokeUserContextCommand : PSCmdlet
{
    private const string ById        = "ById";
    private const string ByUser      = "ByUser";
    private const string CommandAct  = "+Command";
    private const string FileAct     = "+File";
    private const string RedirectAct = "+Redirect";
    private const string VisibleAct  = "+Visible";
    
    private const string ByIdCommand   = ById + CommandAct;
    private const string ByUserCommand = ByUser + CommandAct;
    private const string ByIdFile      = ById + FileAct;
    private const string ByUserFile    = ByUser + FileAct;

    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ByIdCommand)]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ByIdFile)]
    [Alias("Id")]
    public uint SessionId { get; set; } = InteropTypes.INVALID_SESSION_ID;

    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ByUserCommand)]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ByUserFile)]
    [Alias("Name")]
    public string UserName { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ByUserCommand)]
    [Parameter(Mandatory = true, ParameterSetName = ByIdCommand)]
    public string Command { get; set; } = string.Empty;
    
    [Parameter(Mandatory = true, ParameterSetName = ByUserFile)]
    [Parameter(Mandatory = true, ParameterSetName = ByIdFile)]
    [Alias("File")]
    [ArgumentCompleter(typeof(ScriptFileCompleter))]
    public FileInfo FilePath { get; set; }
    
    // todo: support arguments
    
    // Attempting to specify -RedirectOutput together with -ShowWindow will result in a parameter binding error.
    [Parameter] 
    [Alias("Out")] 
    public SwitchParameter RedirectOutput { get; set; }
    
    [Parameter]
    [Alias("Visible")] 
    public SwitchParameter ShowWindow { get; set; }

    private const string RequiredPrivilege = "SeDelegateSessionUserImpersonatePrivilege";
    private const string PowerShellPath    = @"C:\Windows\system32\WindowsPowerShell\v1.0\powershell.exe";
    
    protected override void BeginProcessing()
    {   
        // temporary solution until I source generate parameter sets
        if (ShowWindow.IsPresent && RedirectOutput.IsPresent)
            throw new ParameterBindingException(
                "The parameters -ShowWindow and -RedirectOutput cannot be used together. This is a Win32 limitation.");
        
        if (!TokenExtensions.HasTokenPrivilege(RequiredPrivilege))
            throw new InvalidOperationException(
                "Missing required privilege. You must run this script as SYSTEM or have the SeDelegateSessionUserImpersonatePrivilege token.");

    }
    protected override void ProcessRecord()
    {
        if (!ShouldProcess(Command)) return;

        StringBuilder sbCommand =
            new StringBuilder(
                $"\"{PowerShellPath}\" -ExecutionPolicy Bypass -NoLogo -WindowStyle {(ShowWindow ? "Normal" : "Hidden")}");
        
        if (ParameterSetName.Equals(ByUserFile) || ParameterSetName.Equals(ByIdFile))
        {
            // Should probably copy to session's temp, ensuring the user has access to the file.
            sbCommand.Append(" -File \"{FilePath.FullName}\"");
        }
        else
        {
            var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(Command));
            sbCommand.Append($" -EncodedCommand {encodedCommand}");   
        }

        var primaryToken = ParameterSetName switch
        {
            ByUserFile or ByUserCommand => TokenExtensions.GetSessionUserToken(UserName, false),
            _      => TokenExtensions.GetSessionUserToken(SessionId, false)
        };

        if (primaryToken == null || primaryToken.IsInvalid)
            throw new InvalidOperationException("Failed to get a valid session user token.");

        using (primaryToken)
        {
            var redirectOptions = RedirectOutput
                ? ProcessExtensions.RedirectFlags.Output | ProcessExtensions.RedirectFlags.Error
                : ProcessExtensions.RedirectFlags.None;

            using (var result = ProcessExtensions.CreateProcessAsUser(primaryToken,
                       new ProcessExtensions.ProcessOptions
                       {
                           ApplicationName = PowerShellPath,
                           CommandLine = sbCommand,
                           Redirect = redirectOptions,
                           WindowStyle = ShowWindow ? InteropTypes.SW.SHOW : InteropTypes.SW.HIDE
                       }))
            {
                if (RedirectOutput.IsPresent)
                    WriteObject(new UserProcessWithOutputResult
                    {
                        ProcessId = result.ProcessId,
                        SessionId = SessionId,
                        CommandLine = sbCommand.ToString(),
                        StandardOutput = result.StandardOutput?.ReadToEnd() ?? string.Empty,
                        StandardError = result.StandardError?.ReadToEnd() ?? string.Empty
                    });
                else
                    WriteObject(new UserProcessResult
                    {
                        ProcessId = result.ProcessId,
                        SessionId = SessionId,
                        CommandLine = sbCommand.ToString()
                    });
            }
        }
    }
}

public class UserProcessResult
{
    public uint   ProcessId   { get; set; }
    public uint   SessionId   { get; set; }
    public string CommandLine { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"PID {ProcessId} (Session {SessionId}): {CommandLine}";
    }
}

public class UserProcessWithOutputResult : UserProcessResult
{
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError  { get; set; } = string.Empty;
}