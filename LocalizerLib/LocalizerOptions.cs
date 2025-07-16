namespace InkLocalizer;

public class LocalizerOptions {
	// If true, re-tag everything.
	public bool ReTag = false;

	// Root folder. If empty, uses current working dir.
	public string RootFolder = string.Empty;
	public string DeepSeekKey = string.Empty;
	public bool AutoTranslate = false;
	// Files to include. Will search subfolders of the working dir.
	public string FilePattern = "*.ink";
}