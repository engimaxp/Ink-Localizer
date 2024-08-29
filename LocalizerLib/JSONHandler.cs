using System.Text;
using System.Text.Json;

namespace InkLocalizer;

public class JsonHandler(Localizer localizer, JsonHandler.Options? options = null) {
	public class Options {
		public string OutputFilePath = "";
		public bool Enabled => !string.IsNullOrEmpty(OutputFilePath);
	}

	private readonly Options _options = options ?? new Options();
	private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };

	public bool WriteStrings() {
		string outputFilePath = Path.GetFullPath(_options.OutputFilePath);

		try {
			string fileContents = JsonSerializer.Serialize(localizer.Strings, _serializerOptions);

			File.WriteAllText(outputFilePath, fileContents, Encoding.UTF8);
		}
		catch (Exception ex) {
			Console.Error.WriteLine($"Error writing out JSON file {outputFilePath}: " + ex.Message);
			return false;
		}
		return true;
	}
}