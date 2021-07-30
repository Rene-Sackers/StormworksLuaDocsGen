using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using OfficeOpenXml;

namespace StormworksLuaDocsGen
{
	public class Program
	{
		public static async Task<int> Main(string[] args)
		{
			var rootCommand = new RootCommand
			{
				new Option<string>("--docs-url", "The Git commit author name")
				{
					IsRequired = true
				},
				new Option<FileInfo>("--output", "The path to the directory to monitor and commit in")
				{
					IsRequired = true
				}
			};
			
			rootCommand.Handler = CommandHandler.Create<string, FileInfo>(async (docsUrl, output) =>
			{
				await new ProgramInstance(docsUrl, output).Run();
			});
			
			try
			{
				return await rootCommand.InvokeAsync(args);
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
				return 1;
			}
		}
	}
}