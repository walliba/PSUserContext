using System.Collections.ObjectModel;
using System.IO.Pipes;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Text;
using PSUserContext.Api.Extensions;

namespace PSUserContext.UserHost;

class Program
{
    static void Main(string[] args)
    {
        bool showWindow = true;
        const string PowerShellPath    = @"C:\Windows\system32\WindowsPowerShell\v1.0\powershell.exe";
        
        var consoleId = SessionExtensions.GetActiveConsoleSession();
        
        if (consoleId is null)
        {
            throw new InvalidOperationException("No active console session found.");
        }
        
        var primaryToken = TokenExtensions.GetSessionUserToken(consoleId, false);
        
        if (primaryToken == null || primaryToken.IsInvalid)
            throw new InvalidOperationException("Failed to get a valid session user token.");
        
        StringBuilder sbCommand =
            new StringBuilder(
                $"\"{PowerShellPath}\" -ExecutionPolicy Bypass -NoLogo -OutputFormat XML -WindowStyle {(showWindow ? "Normal" : "Hidden")}");
        
        using (primaryToken)
        {
            var redirectOptions = showWindow
                ? ProcessExtensions.RedirectFlags.None
                : ProcessExtensions.RedirectFlags.Output | ProcessExtensions.RedirectFlags.Error;

            var result = ProcessExtensions.CreateProcessAsUser(primaryToken,
                new ProcessExtensions.ProcessOptions
                {
                    ApplicationName = PowerShellPath,
                    CommandLine = sbCommand,
                    Redirect = redirectOptions,
                    WindowStyle = (ushort)(showWindow ? 5 : 0)
                });

            Console.WriteLine("ProcessId: {0}", result.ProcessId);
            Console.WriteLine("SessionId: {0}", consoleId);
            Console.WriteLine("ExitCode: {0}", result.ExitCode);
        }

        Console.ReadLine();
    }
}