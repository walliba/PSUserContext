using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PSUserContext.Cmdlets
{
	internal static class Helper
	{
		public static Task<string> ReadPipeTask(SafeFileHandle pipeHandle, Encoding encoding)
		{
			return Task.Run(() =>
			{
				var sb = new StringBuilder();

				using (var fs = new FileStream(pipeHandle, FileAccess.Read, 4096, isAsync: false))
				using (var reader = new StreamReader(fs, encoding))
				{
					string line;
					while ((line = reader.ReadLine()) != null)
					{
						sb.AppendLine(line);
					}

					return sb.ToString();
				}
			});
		}
		
	};
	
}
