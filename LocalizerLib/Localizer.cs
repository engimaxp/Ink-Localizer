using Ink;
using Ink.Parsed;
using Object = Ink.Parsed.Object;

namespace InkLocalizer;

public sealed class Localizer(LocalizerOptions? options = null) {
	private readonly LocalizerOptions _localizerOptions = options ?? new LocalizerOptions();

	private static readonly IFileHandler FileHandler = new DefaultFileHandler();

	private readonly HashSet<string> _filesVisited = [];
	public Dictionary<string, string> Strings { private set; get; } = new();

	private IdGenerator _idGenerator = null!;

	public bool Run() {
		string folderPath = GetDirectoryPath();
		// Need this for InkParser to work properly with includes and such.
		Directory.SetCurrentDirectory(folderPath);
		bool success = TryProcessDirectory(folderPath);
		Directory.SetCurrentDirectory(Environment.CurrentDirectory);

		return success;
	}

	private string GetDirectoryPath() {
		string folderPath = _localizerOptions.RootFolder;
		if (string.IsNullOrWhiteSpace(folderPath))
			folderPath = Environment.CurrentDirectory;

		return System.IO.Path.GetFullPath(folderPath);
	}

	private bool TryProcessDirectory(string folderPath) {
		List<string> inkFiles = [];
		try {
			DirectoryInfo dir = new(folderPath);
			inkFiles.AddRange(dir.GetFiles(_localizerOptions.FilePattern, SearchOption.AllDirectories)
				.Select(file => file.FullName));
		}
		catch (Exception ex) {
			Console.Error.WriteLine($"Error finding files to process: {folderPath}: " + ex.Message);
			return false;
		}

		// ----- For each file... -----
		if (!ProcessFiles(inkFiles))
			return false;

		// If new tags need to be added, add them now.
		if (!InsertTagsToFiles())
			return false;

		return true;
	}

	private bool ProcessFiles(List<string> inkFiles) {
		foreach (string inkFile in inkFiles) {
			string? content = FileHandler.LoadInkFileContents(inkFile);
			if (content == null)
				return false;

			try {
				InkParser parser = new(content, inkFile, OnError, FileHandler);
				Story? story = parser.Parse();

				// Go through the parsed story extracting existing localized lines, and lines still to be localized...
				if (!ProcessStory(story)) {
					return false;
				}
			}
			catch (Exception ex) {
				Console.Error.WriteLine($"Error parsing ink file ({inkFile}): {ex.Message}");
				return false;
			}
		}
		return true;
	}

	private static void OnError(string message, ErrorType type) {
		throw new Exception("Ink Parse Error: " + message);
	}

	private bool ProcessStory(Story story) {
		HashSet<string> newFilesVisited = [];

		if (!FindLocalizableText(story, newFilesVisited, out List<Text> validTextObjects))
			return false;

		if (newFilesVisited.Count > 0)
			_filesVisited.UnionWith(newFilesVisited);

		_idGenerator = new IdGenerator(validTextObjects, _localizerOptions);

		// IDs are stored as tags in the form #id:file_knot_stitch_xxxx
		Strings = _idGenerator.GenerateLocalizationIDs(validTextObjects);

		return true;
	}

	private bool FindLocalizableText(Story story, HashSet<string> newFilesVisited, out List<Text> validTextObjects) {
		validTextObjects = [];
		int lastLineNumber = -1;
		foreach (Text? text in story.FindAll<Text>()) {
			if (!IsTextValid(text))
				continue;

			// Have we already visited this source file i.e. is it in an include we've seen before?
			// If so, skip.
			string fileId = System.IO.Path.GetFileNameWithoutExtension(text.debugMetadata.fileName);
			if (_filesVisited.Contains(fileId))
				continue;
			newFilesVisited.Add(fileId);

			// More than one text chunk on a line? We only deal with individual lines of stuff.
			if (lastLineNumber == text.debugMetadata.startLineNumber) {
				Console.Error.WriteLine(
					$"Error in file {fileId} line {lastLineNumber} - two chunks of text when localizer can only work with one per line.");
				return false;
			}
			lastLineNumber = text.debugMetadata.startLineNumber;

			validTextObjects.Add(text);
		}
		return true;
	}

	private static bool IsTextValid(Text text) {
		// Just a newline? Ignore.
		if (text.text.Trim() == string.Empty)
			return false;

		// If it's a tag, ignore.
		if (TagManagement.IsTextTag(text))
			return false;

		// Is this inside some code? In which case we can't do anything with that.
		if (text.parent is VariableAssignment or StringExpression)
			return false;

		return true;
	}

	// Go through every Ink file that needs a tag insertion, and insert!

	private bool InsertTagsToFiles() {
		foreach ((string fileName, List<TagInsert> workList) in _idGenerator.FilesTagsToInsert) {
			if (workList.Count == 0)
				continue;

			Console.WriteLine($"Updating IDs in file: {fileName}");

			if (!TagManagement.InsertTagsToFile(fileName, workList, FileHandler))
				return false;
		}
		return true;
	}
}