using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OfficeOpenXml;

namespace StormworksLuaDocsGen
{
	static class StringExtensions
	{
		public static string RemoveNewlines(this string original)
			=> original.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");
	}
	
	class Function
	{
		public string Name { get; set; }
		
		public string Description { get; set; }

		public List<Parameter> Parameters { get; set; } = new();

		public List<Parameter> ReturnData { get; set; } = new();
	}

	class Parameter
	{
		public string Name { get; set; }
		
		public string Type { get; set; }
		
		public string Description { get; set; }
		
		public bool IsOptional { get; set; }
	}
	
	class Program
	{
		static void Main(string[] args)
		{
			var docsPath = Path.Combine(AppContext.BaseDirectory, "Files\\Docs.xlsx");
			var outputPath = Path.Combine(AppContext.BaseDirectory, "Files\\docs.lua");
			
			ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
			using var package = new ExcelPackage(new FileInfo(docsPath));
			var sheet1 = package.Workbook.Worksheets["Sheet1"];

			var functions = new List<Function>();
			Function function = null;
			for (var row = sheet1.Dimension.Start.Row; row <= sheet1.Dimension.End.Row; row++)
			{
				var cell1 = sheet1.Cells[row, 1].Text;
				if (!string.IsNullOrWhiteSpace(cell1))
				{
					if (function != null)
						functions.Add(function);
					
					function = new()
					{
						Name = cell1,
						Description = sheet1.Cells[row, 2].Text
					};
				}
				
				if (function == null)
					continue;

				var parameterName = sheet1.Cells[row, 4].Text;
				if (string.IsNullOrWhiteSpace(parameterName))
					continue;

				if (parameterName.Contains(" "))
					parameterName = "arg" + (function.Parameters.Count + 1);
				
				var isOptional = sheet1.Cells[row, 3].Text.ToLowerInvariant() == "true";
				var parameterType = sheet1.Cells[row, 5].Text;
				var parameterDescription = sheet1.Cells[row, 6].Text;
				
				function.Parameters.Add(new()
				{
					IsOptional = isOptional,
					Name = parameterName,
					Type = parameterType.Replace("bool", "boolean"),
					Description = parameterDescription
				});
			}

			var stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("---@diagnostic disable: lowercase-global");
			stringBuilder.AppendLine("server = {}");
			stringBuilder.AppendLine("matrix = {}");
			stringBuilder.AppendLine("");
			
			foreach (var docFunction in functions)
			{
				stringBuilder.AppendLine($"--- {docFunction.Description.RemoveNewlines()}");
				foreach (var parameter in docFunction.Parameters)
				{
					stringBuilder.AppendLine($"---@param {parameter.Name} {parameter.Type} {parameter.Description.RemoveNewlines()}");
				}

				var functionParameters = string.Join(", ", docFunction.Parameters.Select(p => p.Name));
				stringBuilder.AppendLine($"function {docFunction.Name}({functionParameters}) end");
				stringBuilder.AppendLine();
			}

			File.WriteAllText(outputPath, stringBuilder.ToString());
			Console.WriteLine("Done");
		}
	}
}