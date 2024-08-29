using System.Text;
using System.Text.Json;

namespace InkLocalizer.TableOutputs;

public class JsonHandler(Localizer localizer, TableOutputOptions? options = null) {
	private readonly TableOutputOptions _options = options ?? new TableOutputOptions();
	private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };

	public void WriteStrings() {
		string outputFilePath = Path.GetFullPath(_options.OutputFilePath);

		try {
			string fileContents = JsonSerializer.Serialize(localizer.Strings, _serializerOptions);

			File.WriteAllText(outputFilePath, fileContents, Encoding.UTF8);
		}
		catch (Exception ex) {
			throw new JsonException($"Error writing out JSON file {outputFilePath}: " + ex.Message);
		}
	}
}