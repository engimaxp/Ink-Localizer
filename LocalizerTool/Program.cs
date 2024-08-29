using InkLocalizer;

Localizer.Options options = new();
CsvHandler.Options csvOptions = new();
JsonHandler.Options jsonOptions = new();

// ----- Simple Args -----
foreach (string arg in args) {
	if (arg.Equals("--retag"))
		options.ReTag = true;
	else if (arg.StartsWith("--folder="))
		options.Folder = arg.Substring(9);
	else if (arg.StartsWith("--filePattern="))
		options.FilePattern = arg.Substring(14);
	else if (arg.StartsWith("--csv="))
		csvOptions.OutputFilePath = arg.Substring(6);
	else if (arg.StartsWith("--json="))
		jsonOptions.OutputFilePath = arg.Substring(7);
	else switch (arg) {
		case "--help":
		case "-h":
			Console.WriteLine("Ink Localizer");
			Console.WriteLine("Arguments:");
			Console.WriteLine(
				"  --folder=<folder> - Root folder to scan for Ink files to localize, relative to working dir.");
			Console.WriteLine("                      e.g. --folder=inkFiles/");
			Console.WriteLine("                      Default is the current working dir.");
			Console.WriteLine("  --filePattern=<folder> - Root folder to scan for Ink files to localize.");
			Console.WriteLine("                           e.g. --filePattern=start-*.ink");
			Console.WriteLine("                           Default is *.ink");
			Console.WriteLine("  --csv=<csvFile> - Path to a CSV file to export, relative to working dir.");
			Console.WriteLine("                    e.g. --csv=output/strings.csv");
			Console.WriteLine("                    Default is empty, so no CSV file will be exported.");
			Console.WriteLine("  --json=<jsonFile> - Path to a JSON file to export, relative to working dir.");
			Console.WriteLine("                      e.g. --json=output/strings.json");
			Console.WriteLine("                      Default is empty, so no JSON file will be exported.");
			Console.WriteLine("  --retag - Regenerate all localisation tag IDs, rather than keep old IDs.");
			return 0;
		case "--test":
			options.Folder = "tests";
			csvOptions.OutputFilePath = "tests/strings.csv";
			jsonOptions.OutputFilePath = "tests/strings.json";
			break;
	}
}

// ----- Parse Ink, Update Tags, Build String List -----
Localizer localizer = new(options);
if (!localizer.Run()) {
	Console.Error.WriteLine("Not localized.");
	return -1;
}
Console.WriteLine($"Localized - found {localizer.Strings.Count} strings.");

// ----- CSV Output -----
if (!string.IsNullOrEmpty(csvOptions.OutputFilePath)) {
	CsvHandler csvHandler = new(localizer, csvOptions);
	if (!csvHandler.WriteStrings()) {
		Console.Error.WriteLine("Database not written.");
		return -1;
	}
	Console.WriteLine($"CSV file written: {csvOptions.OutputFilePath}");
}

// ----- JSON Output -----
if (!string.IsNullOrEmpty(jsonOptions.OutputFilePath)) {
	JsonHandler jsonHandler = new(localizer, jsonOptions);
	if (!jsonHandler.WriteStrings()) {
		Console.Error.WriteLine("Database not written.");
		return -1;
	}
	Console.WriteLine($"JSON file written: {jsonOptions.OutputFilePath}");
}

return 0;