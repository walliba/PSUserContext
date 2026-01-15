using System.Collections.ObjectModel;
using System.IO.Pipes;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Runtime.InteropServices;
using System.Text;
using PSUserContext.Api.Extensions;

namespace PSUserContext.UserHost;

class Program
{
    static void Main(string[] args)
    {
        // if (System.Diagnostics.Debugger.IsAttached)
        //     System.Diagnostics.Debugger.Break();
        
        bool showWindow = true;
        const string PowerShellPath    = @"C:\Windows\system32\WindowsPowerShell\v1.0\powershell.exe";
        
        var consoleId = SessionExtensions.GetConsoleSessionId();
        
        if (consoleId is null)
        {
            throw new InvalidOperationException("No active console session found.");
        }
        
        var primaryToken = TokenExtensions.GetSessionUserToken(consoleId.Value, false);
        
        if (primaryToken == null || primaryToken.IsInvalid)
            throw new InvalidOperationException("Failed to get a valid session user token.");
        
        StringBuilder sbCommand =
            new StringBuilder(
                $"\"{PowerShellPath}\" -ExecutionPolicy Bypass -NoLogo -NonInteractive -WindowStyle {(showWindow ? "Normal" : "Hidden")}");
        
        using (primaryToken)
        {
            var redirectOptions = showWindow
                ? ProcessExtensions.RedirectFlags.None
                : ProcessExtensions.RedirectFlags.Output | ProcessExtensions.RedirectFlags.Error;

            using var result = ProcessExtensions.CreateProcessAsUser(consoleId.Value,
                new ProcessExtensions.ProcessOptions
                {
                    ApplicationName = PowerShellPath,
                    CommandLine = sbCommand,
                    Redirect = redirectOptions,
                    WindowStyle = (ushort)(showWindow ? 5 : 0)
                });
            
            result.ExitCode += i =>
            {
                Console.WriteLine("Exit code: {0}", i);
            };
            
            Console.WriteLine("Process Id: {0}", result.Pid);
            
            NamedPipeConnectionInfo? connectionInfo = new NamedPipeConnectionInfo(Convert.ToInt32(result.Pid));
            
            var ps = PowerShell.Create();
            try
            {
                using var runspace = RunspaceFactory.CreateRunspace(connectionInfo);
                
                runspace.Open();
                ps.Runspace = runspace;
                var results = ps.AddScript("whoami").Invoke();
                foreach (PSObject obj in results)
                {
                    Console.WriteLine("{0}", obj);
                }
                    
                runspace.Close();
                connectionInfo = null;
                Console.ReadLine();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            // Console.WriteLine("ProcessId: {0}", result.ProcessId);
            // Console.WriteLine("SessionId: {0}", consoleId);
            // Console.WriteLine("ExitCode: {0}", result.ExitCode);
        }

        Console.ReadLine();
    }
}