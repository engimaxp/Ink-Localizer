using System.Text;

namespace InkLocalizer;

public class CsvHandler(Localizer localizer, CsvHandler.Options? options = null) {
	public class Options {
		public string OutputFilePath = "";
	}

	private readonly Options _options = options ?? new Options();

	public bool WriteStrings() {
		string outputFilePath = Path.GetFullPath(_options.OutputFilePath);

		try {
			StringBuilder output = new();
			output.AppendLine("ID,Text");

			foreach (string locId in localizer.GetStringKeys()) {
				string textValue = localizer.GetString(locId);
				textValue = textValue.Replace("\"", "\"\"");
				string line = $"{locId},\"{textValue}\"";
				output.AppendLine(line);
			}

			string fileContents = output.ToString();
			File.WriteAllText(outputFilePath, fileContents, Encoding.UTF8);
		}
		catch (Exception ex) {
			Console.Error.WriteLine($"Error writing out CSV file {outputFilePath}: " + ex.Message);
			return false;
		}
		return true;
	}
}