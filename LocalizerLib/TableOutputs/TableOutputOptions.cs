namespace InkLocalizer.TableOutputs;

public class TableOutputOptions {
	public string OutputFilePath = "";
	public bool Enabled => !string.IsNullOrEmpty(OutputFilePath);
}