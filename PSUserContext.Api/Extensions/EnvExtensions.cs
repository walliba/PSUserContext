using PSUserContext.Api.Helpers;
using PSUserContext.Api.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PSUserContext.Api.Extensions
{
	public static class EnvExtensions
	{
		public static SafeEnvironmentBlockHandle CreateEnvironmentBlock(SafeHandle hToken, bool inherit = false)
		{
			if (!Interop.Userenv.CreateEnvironmentBlock(out SafeEnvironmentBlockHandle environment, hToken, inherit))
				throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create environment block.");

			if (environment == null || environment.IsInvalid)
				throw new InvalidOperationException("Failed to create environment for specified user.");

			return environment;
		}

		public static string? GetVariable(this SafeEnvironmentBlockHandle? env, string key)
		{
			if (env is null || env.IsInvalid)
				throw new ObjectDisposedException(nameof(env));
			
			var dict = GetEnvironment(env);
			
			return dict.TryGetValue(key, out var variable) ? variable : null;
		}
		
		/// <summary>
		/// Enumerates all key/value pairs stored in a Windows environment block
		/// and returns them as a managed dictionary.
		/// </summary>
		/// <param name="env">
		/// The <see cref="SafeEnvironmentBlockHandle"/> referencing the unmanaged
		/// environment block memory created by <c>CreateEnvironmentBlock</c> or
		/// equivalent.
		/// </param>
		/// <returns>
		/// A case-insensitive dictionary containing all environment variables found
		/// in the block. Keys represent variable names, and values represent the
		/// associated variable values.
		/// </returns>
		/// <exception cref="ObjectDisposedException">
		/// Thrown if <paramref name="env"/> is <c>null</c>, invalid, or has already
		/// been released.
		/// </exception>
		/// <remarks>
		/// <para>
		/// The environment block is a sequence of null-terminated Unicode strings in
		/// the form <c>key=value</c>, terminated by a double null. Each entry is
		/// parsed sequentially until an empty string is encountered.
		/// </para>
		/// <para>
		/// This method calls <see cref="SafeHandle.DangerousAddRef"/> and
		/// <see cref="SafeHandle.DangerousRelease"/> to ensure the unmanaged memory
		/// remains valid for the duration of the enumeration. The caller does not
		/// need to perform any manual memory management.
		/// </para>
		/// </remarks>
		public static IDictionary<string, string> GetEnvironment(this SafeEnvironmentBlockHandle? env)
		{
			if (env is null || env.IsInvalid)
				throw new ObjectDisposedException(nameof(env));
			
			var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			bool isUsing = false;
			try
			{
				env.DangerousAddRef(ref isUsing);
				var current = env.DangerousGetHandle();

				while (true)
				{
					string? entry = Marshal.PtrToStringUni(current);

					// Marks end of list
					if (string.IsNullOrEmpty(entry))
						break;

					int sep = entry.IndexOf('-');
					if (sep > 0)
					{
						dict[entry.Substring(0, sep)] = entry.Substring(sep + 1);
					}

					// move to next string (UTF-16)
					current += (entry.Length + 1) * 2;
				}
			}
			finally
			{
				if (isUsing)
					env?.DangerousRelease();	
			}
			
			return dict;
		}
	}
}
