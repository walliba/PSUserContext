using PSUserContext.Api.Helpers;
using PSUserContext.Api.Interop;
using System;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Text;
using PSUserContext.Api.Extensions;

namespace PSUserContext.Cmdlets
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

	[Cmdlet(VerbsLifecycle.Invoke, "UserContext", DefaultParameterSetName = "ById", SupportsShouldProcess = true)]
	[OutputType(typeof(UserProcessResult))]
	[OutputType(typeof(UserProcessWithOutputResult))]
	public sealed class InvokeUserContextCommand : PSCmdlet
	{
		private const string ById = "ById";
		private const string ByUser = "ByUser";

		[Parameter(Mandatory = true, Position = 0, ParameterSetName = ById)]
		[Alias("Id")]
		public uint SessionId { get; set; } = InteropTypes.INVALID_SESSION_ID;

		[Parameter(Mandatory = true, Position = 0, ParameterSetName = ByUser)]
		[Alias("Name")]
		public string UserName { get; set; } = string.Empty;

		[Parameter(Mandatory = true, Position = 1)]
		public string Command { get; set; } = string.Empty;

		// This parameter belongs only to the Redirected parameter set.
		// Attempting to specify -RedirectOutput together with -ShowWindow will result in a parameter binding error.
		[Parameter()]
		[Alias("Out")]
		public SwitchParameter RedirectOutput { get; set; }

		// This parameter belongs only to the Visible parameter set.
		[Parameter()]
		[Alias("Visible")]
		public SwitchParameter ShowWindow { get; set; }

		private const int BUFFSZ = 4096;
		protected override void ProcessRecord()
		{
			if (ShowWindow.IsPresent && RedirectOutput.IsPresent)
				throw new ParameterBindingException("The parameters -ShowWindow and -RedirectOutput cannot be used together.");

			if (!ShouldProcess(Command)) return;

			string PowerShellPath = "C:\\Windows\\system32\\WindowsPowerShell\\v1.0\\powershell.exe";

			var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(Command));

			var sbCommand = new StringBuilder($"\"{PowerShellPath}\" -ExecutionPolicy Bypass -NoLogo -WindowStyle {(ShowWindow ? "Normal" : "Hidden")} -EncodedCommand {encodedCommand}");

			// Retrieve token privileges dictionary
			var privileges = TokenExtensions.GetTokenPrivileges();

			// Try to get the specific privilege
			if (!privileges.TryGetValue("SeDelegateSessionUserImpersonatePrivilege", out var privilegeAttr) ||
				(privilegeAttr == InteropTypes.PrivilegeAttributes.Disabled))
			{
				throw new InvalidOperationException(
					"Not running with correct privilege. You must run this script as SYSTEM " +
					"or have the SeDelegateSessionUserImpersonatePrivilege token.");
			}

			SafeNativeHandle primaryToken;

			switch (ParameterSetName)
			{
				case ByUser:
					primaryToken = TokenExtensions.GetSessionUserToken(UserName, false);
					break;
				default:
					primaryToken = TokenExtensions.GetSessionUserToken(SessionId, false);
					break;
			}

			if (primaryToken == null || primaryToken.IsInvalid)
			{
				throw new InvalidOperationException("Failed to get a valid session user token.");
			}

			using (primaryToken)
			{
				
				var redirectOptions = (RedirectOutput ? ProcessExtensions.RedirectFlags.Output | ProcessExtensions.RedirectFlags.Error : ProcessExtensions.RedirectFlags.None);

				using (var result = ProcessExtensions.CreateProcessAsUser(primaryToken,
					       new ProcessExtensions.ProcessOptions
					       {
						       ApplicationName = PowerShellPath,
						       CommandLine = sbCommand,
						       Redirect = redirectOptions,
						       WindowStyle = (ShowWindow ? InteropTypes.SW.SHOW : InteropTypes.SW.HIDE)
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
							CommandLine = sbCommand.ToString(),
						});	
				}
			}
		}
	}

	public class UserProcessResult
	{
		public uint ProcessId { get; set; }
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
