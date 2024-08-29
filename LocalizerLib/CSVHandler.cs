using System.Text;

namespace InkLocalizer;

public class CsvHandler(Localizer localizer, TableOutputOptions? options = null) {
	private readonly TableOutputOptions _options = options ?? new TableOutputOptions();

	public bool WriteStrings() {
		string outputFilePath = Path.GetFullPath(_options.OutputFilePath);

		try {
			StringBuilder output = new();
			output.AppendLine("ID,Text");

			foreach (KeyValuePair<string, string> locStr in localizer.Strings) {
				string textValue = locStr.Value;
				textValue = textValue.Replace("\"", "\"\"");
				string line = $"{locStr.Key},\"{textValue}\"";
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