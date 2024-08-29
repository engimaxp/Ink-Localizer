namespace InkLocalizer;

internal abstract class Program {

	private static readonly LocalizerOptions LocalizerOptions = new();
	private static readonly TableOutputOptions CsvOptions = new();
	private static readonly TableOutputOptions JsonOptions = new();

	private static int Main(string[] args) {
		if (ProcessArgs(args))
			return 0;

		Localizer localizer = new(LocalizerOptions);
		if (!localizer.Run()) {
			Console.Error.WriteLine("Not localized.");
			return -1;
		}
		Console.WriteLine($"Localized - found {localizer.Strings.Count} strings.");

		if (CsvOptions.Enabled && !TryExportCsv(localizer)) {
			Console.Error.WriteLine("CSV not written.");
			return -1;
		}

		if (JsonOptions.Enabled && !TryExportJson(localizer)) {
			Console.Error.WriteLine("JSON not written.");
			return -1;
		}

		return 0;
	}

	private static bool TryExportCsv(Localizer localizer) {
		CsvHandler csvHandler = new(localizer, CsvOptions);
		if (!csvHandler.WriteStrings()) {
			return false;
		}
		Console.WriteLine($"CSV file written: {CsvOptions.OutputFilePath}");
		return true;
	}

	private static bool TryExportJson(Localizer localizer) {
		JsonHandler jsonHandler = new(localizer, JsonOptions);
		if (!jsonHandler.WriteStrings()) {
			return false;
		}
		Console.WriteLine($"JSON file written: {JsonOptions.OutputFilePath}");
		return true;
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
			LocalizerOptions.RootFolder = "tests";
			CsvOptions.OutputFilePath = "tests/strings.csv";
			JsonOptions.OutputFilePath = "tests/strings.json";
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