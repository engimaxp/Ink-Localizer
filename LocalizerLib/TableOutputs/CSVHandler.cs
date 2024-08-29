using System.Text;

namespace InkLocalizer.TableOutputs;

public class CsvHandler(Localizer localizer, TableOutputOptions? options = null) {
	private readonly TableOutputOptions _options = options ?? new TableOutputOptions();

	public void WriteStrings() {
		string outputFilePath = Path.GetFullPath(_options.OutputFilePath);

		try {
			StringBuilder output = new();
			output.AppendLine("ID,Text");

			foreach ((string id, string text) in localizer.Strings) {
				string textValue = text;
				textValue = textValue.Replace("\"", "\"\"");
				string line = $"{id},\"{textValue}\"";
				output.AppendLine(line);
			}

			string fileContents = output.ToString();
			File.WriteAllText(outputFilePath, fileContents, Encoding.UTF8);
		}
		catch (Exception ex) {
			throw new Exception($"Error writing out CSV file {outputFilePath}: " + ex.Message);
		}
	}
}