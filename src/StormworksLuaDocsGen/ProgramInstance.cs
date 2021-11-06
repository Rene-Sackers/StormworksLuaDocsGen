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
	public class TypeDefinition
	{
		public string Name { get; }

		public List<(string fieldName, string type, string description)> Fields { get; } = new();

		public TypeDefinition(string name)
		{
			Name = name;
		}
	}
	
	public class ProgramInstance : IDisposable
	{
		private const string GoogleSheetExportToXlsxSuffix = "/export?format=xlsx";
		
		private readonly string _docsUrl;
		private readonly FileInfo _outputFilePath;

		private ExcelPackage _currentPackage;

		public ProgramInstance(string docsUrl, FileInfo outputFilePath)
		{
			_docsUrl = docsUrl;
			_outputFilePath = outputFilePath;
		}

		public async Task Run()
		{
			var excelExportFilePath = Path.Combine(AppContext.BaseDirectory, "export.xlsx");

			await DownloadDocsExcelExport(excelExportFilePath);

			ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
			_currentPackage = new ExcelPackage(new FileInfo(excelExportFilePath));

			var functionDescriptionsSheet = _currentPackage.Workbook.Worksheets["Function Descriptions"];
			if (functionDescriptionsSheet == null)
			{
				Console.WriteLine("No \"Function Descriptions\" worksheet in Excel file");
				return;
			}
			
			var returnTypesSheet = _currentPackage.Workbook.Worksheets["Return Types"];
			List<TypeDefinition> typeDefinitions = new(0);
			if (returnTypesSheet == null)
				Console.WriteLine("No \"Return Types\" worksheet in Excel file");
			else
				typeDefinitions = ExtractTypeDefinitions(returnTypesSheet).ToList();

			Console.WriteLine("Extracting functions");
			var functions = ExtractFunctions(functionDescriptionsSheet);

			var docsStringBuilder = GenerateDocs(typeDefinitions, functions);

			Console.WriteLine($"Writing to {_outputFilePath.FullName}");
			await File.WriteAllTextAsync(_outputFilePath.FullName, docsStringBuilder.ToString());
			Console.WriteLine("Done");
		}

		private async Task DownloadDocsExcelExport(string path)
		{
			var exportUrl = _docsUrl.TrimEnd('/') + GoogleSheetExportToXlsxSuffix;
			Console.WriteLine($"Downloading {exportUrl} to {path}");

			using var httpClient = new HttpClient();

			var downloadStream = await httpClient.GetStreamAsync(exportUrl);
			await using var destinationFileSteam = File.Open(path, FileMode.Create);
			await downloadStream.CopyToAsync(destinationFileSteam);

			Console.WriteLine($"Downloaded file. Bytes: {destinationFileSteam.Length}");
			await downloadStream.FlushAsync();
			await destinationFileSteam.FlushAsync();
			destinationFileSteam.Close();
			await destinationFileSteam.DisposeAsync();
		}

		private static IEnumerable<TypeDefinition> ExtractTypeDefinitions(ExcelWorksheet typeDefinitionsSheet)
		{
			Console.WriteLine("Extracting types");
			for (var row = typeDefinitionsSheet.Dimension.Start.Row; row <= typeDefinitionsSheet.Dimension.End.Row; row++)
			{
				var typeName = typeDefinitionsSheet.Cells[row, 1].Text;
				if (string.IsNullOrWhiteSpace(typeName))
					break;
				
				Console.WriteLine("Type: " + typeName);

				typeName = typeName.Replace(" ", null);
				var definition = new TypeDefinition(typeName);
				row += 2; // Skip Field/Type/Description headers

				do
				{
					var fieldName = typeDefinitionsSheet.Cells[row, 1].Text;
					if (string.IsNullOrWhiteSpace(fieldName))
						break;
					
					var fieldType = typeDefinitionsSheet.Cells[row, 2].Text;
					var fieldDescription = typeDefinitionsSheet.Cells[row, 3].Text;

					var fieldIsIndex = int.TryParse(fieldName, out _);
					if (fieldIsIndex)
						fieldName = $"[{fieldName}]";
					
					definition.Fields.Add((fieldName, fieldType, fieldDescription));
					Console.WriteLine($"\t{ fieldName} {fieldType} - {fieldDescription}");
					row++;
				} while (true);

				yield return definition;
			}
		}

		private List<Function> ExtractFunctions(ExcelWorksheet functionDescriptionsSheet)
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

			// Add last function
			functions.Add(function);

			return functions;
		}

		private void ParseParameter(ExcelWorksheet functionDescriptionsSheet, int row, Function function)
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

		private string TranslateHyperlink(ExcelRange cell)
		{
			var descriptionHyperlink = cell.Hyperlink as ExcelHyperLink;
			if (descriptionHyperlink == null)
				return null;

			if (string.IsNullOrWhiteSpace(descriptionHyperlink.ReferenceAddress) || !_currentPackage.Workbook.Names.ContainsKey(descriptionHyperlink.ReferenceAddress))
				return descriptionHyperlink.AbsoluteUri;

			var referredCell = _currentPackage.Workbook.Names[descriptionHyperlink.ReferenceAddress].Address.Replace("!", ": ").Replace("$", null);
			return $"(Refer to {referredCell} on {_docsUrl})";
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

		private StringBuilder GenerateDocs(List<TypeDefinition> typeDefinitions, ICollection<Function> functions)
		{
			Console.WriteLine("Generating docs");

			var stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("-- Auto generated docs by StormworksLuaDocsGen (https://github.com/Rene-Sackers/StormworksLuaDocsGen)");
			stringBuilder.AppendLine($"-- Based on data in: {_docsUrl}");
			stringBuilder.AppendLine($"-- Notice issues/missing info? Please contribute here: {_docsUrl}, then create an issue on the GitHub repo");
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("--- @diagnostic disable: lowercase-global");
			stringBuilder.AppendLine();
			functions
				.Select(f => f.Name.Split(".").FirstOrDefault())
				.Distinct()
				.Where(f => !string.IsNullOrWhiteSpace(f))
				.ToList()
				.ForEach(f => stringBuilder.AppendLine($"{f} = {{}}"));
			stringBuilder.AppendLine();

			foreach (var typeDefinition in typeDefinitions)
			{
				Console.WriteLine($"Writing type {typeDefinition.Name}");
				stringBuilder.AppendLine($"--- @class {typeDefinition.Name}");

				foreach (var field in typeDefinition.Fields)
				{
					stringBuilder.AppendLine($"--- @field {field.fieldName} {MapType(field.type)} {field.description}");
				}
				stringBuilder.AppendLine();
			}

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

		public void Dispose()
		{
			_currentPackage?.Dispose();
		}
	}
}