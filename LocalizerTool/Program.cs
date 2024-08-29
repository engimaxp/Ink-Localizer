using System.Diagnostics;
using InkLocalizer.TableOutputs;

namespace InkLocalizer;

internal abstract class Program {
	private const string DebuggingTestPath = @"E:\GitHub\Ink-Localiser\LocalizerTool\tests";

	private static readonly LocalizerOptions LocalizerOptions = new();
	private static readonly TableOutputOptions CsvOptions = new();
	private static readonly TableOutputOptions JsonOptions = new();

	private static int Main(string[] args) {
		if (ProcessArgs(args))
			return 0;

		Localizer localizer = new(LocalizerOptions);
		localizer.Run();
		Console.WriteLine($"Localized - found {localizer.Strings.Count} strings.");

		if (CsvOptions.Enabled)
			ExportCsv(localizer);
		if (JsonOptions.Enabled)
			ExportJson(localizer);

		return 0;
	}

	private static void ExportCsv(Localizer localizer) {
		CsvHandler csvHandler = new(localizer, CsvOptions);
		csvHandler.WriteStrings();
		Console.WriteLine($"CSV file written: {CsvOptions.OutputFilePath}");
	}

	private static void ExportJson(Localizer localizer) {
		JsonHandler jsonHandler = new(localizer, JsonOptions);
		jsonHandler.WriteStrings();
		Console.WriteLine($"JSON file written: {JsonOptions.OutputFilePath}");
	}

	private static bool ProcessArgs(string[] args) => args.Any(ProcessArg);
	private static bool ProcessArg(string arg) {
		if (arg.Equals("--retag")) {
			LocalizerOptions.ReTag = true;
			return false;
		}
		if (arg.StartsWith("--folder=")) {
			LocalizerOptions.RootFolder = arg.Substring(9);
			return false;
		}
		if (arg.StartsWith("--filePattern=")) {
			LocalizerOptions.FilePattern = arg.Substring(14);
			return false;
		}
		if (arg.StartsWith("--csv=")) {
			CsvOptions.OutputFilePath = arg.Substring(6);
			return false;
		}
		if (arg.StartsWith("--json=")) {
			JsonOptions.OutputFilePath = arg.Substring(7);
			return false;
		}
		if (arg.StartsWith("--test")) {
			LocalizerOptions.RootFolder = DebuggingTestPath;
			CsvOptions.OutputFilePath = @$"{DebuggingTestPath}\strings.csv";
			JsonOptions.OutputFilePath = @$"{DebuggingTestPath}\strings.json";
			return false;
		}

		Console.WriteLine(
			"""
			Ink Localizer Usage
			
			Arguments:
			--folder=<folder> - Root folder to scan for Ink files to localize, relative to working dir.
			                    e.g. --folder=inkFiles/
			                    Default is the current working dir.

			--filePattern=<folder> - Root folder to scan for Ink files to localize.
			                         e.g. --filePattern=start-*.ink
			                         Default is *.ink

			--csv=<csvFile> - Path to a CSV file to export, relative to working dir.
			                  e.g. --csv=output/strings.csv
			                  Default is empty, so no CSV file will be exported.

			--json=<jsonFile> - Path to a JSON file to export, relative to working dir.
			                    e.g. --json=output/strings.json
			                    Default is empty, so no JSON file will be exported.

			--retag - Regenerate all localisation tag IDs, rather than keep old IDs.
			""");
		return true;
	}
}