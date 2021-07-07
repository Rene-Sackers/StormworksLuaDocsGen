using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using OfficeOpenXml;

namespace StormworksLuaDocsGen
{
	public static class StringExtensions
	{
		public static string RemoveNewlines(this string original)
			=> original.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");
	}
	
	public class Function
	{
		public string Name { get; set; }
		
		public string Description { get; set; }

		public List<Parameter> Parameters { get; set; } = new();

		public List<Parameter> ReturnParameters { get; set; } = new();
	}

	public class Parameter
	{
		public string Name { get; set; }
		
		public string Type { get; set; }
		
		public string Description { get; set; }
		
		public bool IsOptional { get; set; }
	}
	
	public class Program
	{
		private const string DocsGoogleSheetUrl = "https://docs.google.com/spreadsheets/d/1DkjUjX6DwCBt8IhA43NoYhtxk42_f6JXb-dfxOX9lgg/";
		private const string DocsGoogleSheetExportUrl = "https://docs.google.com/spreadsheets/d/1DkjUjX6DwCBt8IhA43NoYhtxk42_f6JXb-dfxOX9lgg/export?format=xlsx"; 
		
		public static async Task Main(string[] args)
		{
			try
			{
				await Run();
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}
		}

		private static async Task Run()
		{
			Console.WriteLine("Stormworks Lua docs generator");
			
			var inputPath = Path.Combine(AppContext.BaseDirectory, "docs.xlsx");
			var outputPath = Path.Combine(AppContext.BaseDirectory, "docs.lua");
			
			await DownloadDocsExcelExport(inputPath);

			ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
			using var package = new ExcelPackage(new FileInfo(inputPath));
			
			var functionDescriptionsSheet = package.Workbook.Worksheets["Function Descriptions"];
			if (functionDescriptionsSheet == null)
			{
				Console.WriteLine("No \"Function Descriptions\" worksheet in Excel file");
				return;
			}

			Console.WriteLine("Extracting functions");
			
			var functions = ExtractFunctions(functionDescriptionsSheet);

			var docsStringBuilder = GenerateDocs(functions);

			Console.WriteLine($"Writing to {outputPath}");
			await File.WriteAllTextAsync(outputPath, docsStringBuilder.ToString());
			Console.WriteLine("Done");
		}

		private static async Task DownloadDocsExcelExport(string path)
		{
			if (File.Exists(path) && DateTime.Now - new FileInfo(path).CreationTime <= TimeSpan.FromHours(1))
			{
				Console.WriteLine("Skipping download, existing file is < 1 hour old");
				return;
			}
			
			Console.WriteLine($"Downloading {DocsGoogleSheetExportUrl} to {path}");
			
			using var httpClient = new HttpClient();

			var downloadStream = await httpClient.GetStreamAsync(DocsGoogleSheetExportUrl);
			await using var destinationFileSteam = File.Open(path, FileMode.Create);
			await downloadStream.CopyToAsync(destinationFileSteam);

			Console.WriteLine($"Downloaded file. Bytes: {destinationFileSteam.Length}");
			await downloadStream.FlushAsync();
			await destinationFileSteam.FlushAsync();
			destinationFileSteam.Close();
			await destinationFileSteam.DisposeAsync();
		}

		private static List<Function> ExtractFunctions(ExcelWorksheet functionDescriptionsSheet)
		{
			var functions = new List<Function>();
			Function function = null;
			for (var row = functionDescriptionsSheet.Dimension.Start.Row + 1; row <= functionDescriptionsSheet.Dimension.End.Row; row++)
			{
				var functionName = functionDescriptionsSheet.Cells[row, 2].Text;
				if (!string.IsNullOrWhiteSpace(functionName))
				{
					Console.WriteLine();

					if (function != null)
						functions.Add(function);

					function = new()
					{
						Name = functionName,
						Description = functionDescriptionsSheet.Cells[row, 3].Text
					};

					Console.WriteLine($"Found function {functionName}");
				}

				if (function == null)
					continue;

				ParseParameter(functionDescriptionsSheet, row, function);
				ParseReturnParameter(functionDescriptionsSheet, row, function);
			}

			return functions;
		}

		private static void ParseParameter(ExcelWorksheet functionDescriptionsSheet, int row, Function function)
		{
			var parameterName = functionDescriptionsSheet.Cells[row, 5].Text;
			if (string.IsNullOrWhiteSpace(parameterName))
				return;

			Console.WriteLine($"\tFound parameter {parameterName}");

			if (parameterName.Contains(" "))
			{
				Console.WriteLine($"\tFound invalid parameter name: {parameterName} on row {row}");
				parameterName = "arg" + (function.Parameters.Count + 1);
			}

			var isOptional = functionDescriptionsSheet.Cells[row, 4].Text.ToLowerInvariant() == "1";
			var parameterType = functionDescriptionsSheet.Cells[row, 6].Text;

			var descriptionCell = functionDescriptionsSheet.Cells[row, 7];
			var translatedHyperlink = TranslateHyperlink(descriptionCell);
			var parameterDescription = descriptionCell.Text + (string.IsNullOrWhiteSpace(translatedHyperlink) ? null : $" {translatedHyperlink}");

			function.Parameters.Add(new()
			{
				IsOptional = isOptional,
				Name = parameterName,
				Type = MapType(parameterType),
				Description = parameterDescription
			});
		}

		private static string TranslateHyperlink(ExcelRange cell)
		{
			var descriptionHyperlink = cell.Hyperlink as ExcelHyperLink;
			if (descriptionHyperlink == null)
				return null;

			return !string.IsNullOrWhiteSpace(descriptionHyperlink.ReferenceAddress) ?
				$"(Refer to cells \"{descriptionHyperlink.ReferenceAddress}\" on {DocsGoogleSheetUrl})"
				: descriptionHyperlink.AbsoluteUri;
		}

		private static void ParseReturnParameter(ExcelWorksheet functionDescriptionsSheet, int row, Function function)
		{
			var returnParameterName = functionDescriptionsSheet.Cells[row, 8].Text;
			if (string.IsNullOrWhiteSpace(returnParameterName))
				return;

			Console.WriteLine($"\tFound return parameter {returnParameterName}");

			if (returnParameterName.Contains(" "))
			{
				Console.WriteLine($"\tFound invalid parameter name: {returnParameterName} on row {row}");
				returnParameterName = "arg" + (function.Parameters.Count + 1);
			}

			var parameterType = functionDescriptionsSheet.Cells[row, 9].Text;
			var parameterDescription = functionDescriptionsSheet.Cells[row, 10].Text;

			function.ReturnParameters.Add(new()
			{
				Name = returnParameterName,
				Type = MapType(parameterType),
				Description = parameterDescription
			});
		}

		private static string MapType(string type)
		{
			return type.Replace("bool", "boolean");
		}

		private static StringBuilder GenerateDocs(IEnumerable<Function> functions)
		{
			Console.WriteLine("Generating docs");

			var stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("-- Auto generated docs by StormworksLuaDocsGen (https://github.com/Rene-Sackers/StormworksLuaDocsGen)");
			stringBuilder.AppendLine($"-- Based on data in: {DocsGoogleSheetUrl}");
			stringBuilder.AppendLine($"-- Notice issues/missing info? Please contribute here: {DocsGoogleSheetUrl}, then create an issue on the GitHub repo");
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("--- @diagnostic disable: lowercase-global");
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("server = {}");
			stringBuilder.AppendLine("matrix = {}");
			stringBuilder.AppendLine();

			foreach (var docFunction in functions)
			{
				Console.WriteLine($"Writing function {docFunction.Name}");

				stringBuilder.AppendLine($"--- {docFunction.Description.RemoveNewlines()}");
				foreach (var parameter in docFunction.Parameters)
				{
					var type = parameter.IsOptional ? $"{parameter.Type}|nil" : parameter.Type;
					stringBuilder.AppendLine($"--- @param {parameter.Name} {type} {parameter.Description.RemoveNewlines()}");
				}

				if (docFunction.ReturnParameters.Any())
				{
					var returnParameterTypes = string.Join(", ", docFunction.ReturnParameters.Select(rp => $"{rp.Type} {rp.Name}"));
					stringBuilder.AppendLine($"--- @return {returnParameterTypes}");
				}

				var functionParameters = string.Join(", ", docFunction.Parameters.Select(p => p.Name));
				stringBuilder.AppendLine($"function {docFunction.Name}({functionParameters}) end");
				stringBuilder.AppendLine();
			}

			return stringBuilder;
		}
	}
}