using Microsoft.Win32.SafeHandles;
using PSUserContext.Api.Helpers;
using PSUserContext.Api.Native;
using PSUserContext.Cmdlets;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using static PSUserContext.Api.Native.Advapi32;
using static PSUserContext.Api.Native.InteropTypes;
using static PSUserContext.Api.Services.ProcessExtensions;

namespace PSUserContext.Cmdlet
{
	public class Win32Exception : System.ComponentModel.Win32Exception
	{
		private string _msg;
		public Win32Exception(string message) : this(Marshal.GetLastWin32Error(), message) { }
		public Win32Exception(int errorCode, string message) : base(errorCode)
		{
			_msg = String.Format("{0} ({1}, Win32ErrorCode {2} - 0x{2:X8})", message, base.Message, errorCode);
		}
		public override string Message { get { return _msg; } }
		public static explicit operator Win32Exception(string message) { return new Win32Exception(message); }
	}

	[Cmdlet(VerbsLifecycle.Start, "UserProcess", DefaultParameterSetName = "Default", SupportsShouldProcess = true)]
	[OutputType(typeof(UserProcessResult))]
	[OutputType(typeof(UserProcessWithOutputResult))]
	public sealed class StartUserProcessCommand : PSCmdlet
	{
		private const string DefaultSet = "Default";
		private const string RedirectedSet = "Redirected";
		private const string VisibleSet = "Visible";

		[Parameter(Mandatory = true, Position = 0, ParameterSetName = DefaultSet)]
		[Parameter(Mandatory = true, Position = 0, ParameterSetName = RedirectedSet)]
		[Parameter(Mandatory = true, Position = 0, ParameterSetName = VisibleSet)]
		public string CommandLine { get; set; } = string.Empty;

		[Parameter(Position = 1, ParameterSetName = DefaultSet)]
		[Parameter(Position = 1, ParameterSetName = RedirectedSet)]
		[Parameter(Position = 1, ParameterSetName = VisibleSet)]
		public uint SessionId { get; set; } = 1;

		// This parameter belongs only to the Redirected parameter set.
		// Attempting to specify -RedirectOutput together with -ShowWindow will result in a parameter binding error.
		[Parameter(ParameterSetName = RedirectedSet)]
		public SwitchParameter RedirectOutput { get; set; }

		// This parameter belongs only to the Visible parameter set.
		[Parameter(ParameterSetName = VisibleSet)]
		public SwitchParameter ShowWindow { get; set; }

		private const int BUFFSZ = 4096;
		protected override void ProcessRecord()
		{
			if (!ShouldProcess(CommandLine)) return;

			SECURITY_ATTRIBUTES saAttr = new SECURITY_ATTRIBUTES
			{
				nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
				bInheritHandle = 0x1,
				lpSecurityDescriptor = IntPtr.Zero,
			};

			if (!Kernel32.CreatePipe(out var out_read, out var out_write, ref saAttr, 0))
				throw new InvalidOperationException("failed to create output pipe");
			if (!Kernel32.CreatePipe(out var err_read, out var err_write, ref saAttr, 0))
				throw new InvalidOperationException("failed to create error pipe");
			if (!Kernel32.SetHandleInformation(out_read, HANDLE_FLAG_INHERIT, 0))
				throw new InvalidOperationException("failed to set out read handle info");
			if (!Kernel32.SetHandleInformation(err_read, HANDLE_FLAG_INHERIT, 0))
				throw new InvalidOperationException("failed to set err read handle info");
			if (!Kernel32.SetHandleInformation(out_write, HANDLE_FLAG_INHERIT, HANDLE_FLAG_INHERIT))
				throw new InvalidOperationException("failed to set out write handle info");
			if (!Kernel32.SetHandleInformation(err_write, HANDLE_FLAG_INHERIT, HANDLE_FLAG_INHERIT))
				throw new InvalidOperationException("failed to set err write handle info");

			string PowerShellPath = "C:\\Windows\\system32\\WindowsPowerShell\\v1.0\\powershell.exe";

			var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(CommandLine));

			CommandLine = $"\"{PowerShellPath}\" -ExecutionPolicy Bypass -NoLogo -WindowStyle {(ShowWindow ? "Normal" : "Hidden")} -EncodedCommand {encodedCommand}";

			// Retrieve token privileges dictionary
			var privileges = GetTokenPrivileges();

			// Try to get the specific privilege
			if (!privileges.TryGetValue("SeDelegateSessionUserImpersonatePrivilege", out var privilegeAttr) ||
				(privilegeAttr == PrivilegeAttributes.Disabled))
			{
				throw new InvalidOperationException(
					"Not running with correct privilege. You must run this script as SYSTEM " +
					"or have the SeDelegateSessionUserImpersonatePrivilege token.");
			}

			// Set up STARTUPINFOEX
			PROCESS_INFORMATION pi;
			var si = new STARTUPINFO
			{
				cb = (uint)Marshal.SizeOf<STARTUPINFO>(),
				lpDesktop = "winsta0\\default",
				wShowWindow = ShowWindow ? SW.SW_SHOW : SW.SW_HIDE,
				hStdOutput = out_write,
				hStdError = err_write,
				dwFlags = STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW,
			};

			// Creation flags
			uint dwCreationFlags = CREATE_UNICODE_ENVIRONMENT;

			if (!RedirectOutput)
				dwCreationFlags |= CREATE_NEW_CONSOLE;

			var primaryToken = GetSessionUserToken(false);

			if (primaryToken == null || primaryToken.IsInvalid)
			{
				throw new InvalidOperationException("Failed to get a valid session user token.");
			}

			using (primaryToken)
			{
				// Create environment block
				if (!Userenv.CreateEnvironmentBlock(out SafeEnvironmentBlockHandle environment, primaryToken, false))
					throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateEnvironmentBlock failed.");

				if (environment == null || environment.IsInvalid)
					throw new InvalidOperationException("environment handle is invalid");

				using (environment)
				{
					WriteVerbose("Envblock handle created");

					WriteVerbose("Launching process");
					// Launch process
					if (!Advapi32.CreateProcessAsUserW(
						primaryToken,
						PowerShellPath,
						new StringBuilder(CommandLine),
						IntPtr.Zero,
						IntPtr.Zero,
						RedirectOutput.ToBool(),
						dwCreationFlags,
						environment,
						"C:\\Windows\\System32",
						ref si,
						out pi))
					{
						throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcessAsUser failed");
					}

					out_write?.Dispose();
					err_write?.Dispose();

					try
					{
						Kernel32.WaitForSingleObject(pi.hProcess, Kernel32.INFINITE);
					}
					finally
					{
						Kernel32.CloseHandle(pi.hThread);
						Kernel32.CloseHandle(pi.hProcess);
					}
				}
			}

			if (RedirectOutput)
			{
				var stdOutTask = Helper.ReadPipeTask(out_read, Encoding.Default);
				var stdErrTask = Helper.ReadPipeTask(err_read, Encoding.Default);

				Task.WaitAll(stdOutTask, stdErrTask);

				WriteObject(new UserProcessWithOutputResult
				{
					ProcessId = pi.dwProcessId,
					ThreadId = pi.dwThreadId,
					SessionId = SessionId,
					CommandLine = CommandLine,
					StandardOutput = stdOutTask.Result,
					StandardError = stdErrTask.Result,
				});
			} 
			else
			{
				WriteObject(new UserProcessResult
				{
					ProcessId = pi.dwProcessId,
					ThreadId = pi.dwThreadId,
					SessionId = SessionId,
					CommandLine = CommandLine
				});
			}
			out_read?.Dispose();
			err_read?.Dispose();
		}
	}

	public class UserProcessResult
	{
		public int ProcessId { get; set; }
		public int ThreadId { get; set; }
		public uint SessionId { get; set; }
		public string CommandLine { get; set; } = string.Empty;
		public override string ToString() => $"PID {ProcessId} (Session {SessionId}): {CommandLine}";
	}

	public class UserProcessWithOutputResult : UserProcessResult
	{
		public string StandardOutput { get; set; } = String.Empty;
		public string StandardError { get; set; } = String.Empty;
	}
}
