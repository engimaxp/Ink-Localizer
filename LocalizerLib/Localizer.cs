using Ink;
using Ink.Parsed;
using InkLocalizer.Helper;
using static InkLocalizer.TagManagement;

namespace InkLocalizer;

public sealed class Localizer {
	private readonly LocalizerOptions _localizerOptions;
	private readonly IdGenerator _idGenerator;
	private static readonly IFileHandler FileHandler = new DefaultFileHandler();

	private readonly HashSet<string> _filesVisited = [];
	public Dictionary<string, string> Strings { get; } = new();

	public Localizer(LocalizerOptions? options = null) {
		_localizerOptions = options ?? new LocalizerOptions();
		_idGenerator = new IdGenerator(_localizerOptions);
	}

	public void Run() {
		string folderPath = GetDirectoryPath();
		if (!Directory.Exists(folderPath))
			throw new DirectoryNotFoundException($"Directory \"{folderPath}\" does not exist.");

		// Need this for InkParser to work properly with includes and such.
		Directory.SetCurrentDirectory(folderPath);
		ProcessDirectory(folderPath);
		Directory.SetCurrentDirectory(Environment.CurrentDirectory);
	}

	private string GetDirectoryPath() {
		string folderPath = _localizerOptions.RootFolder;
		if (string.IsNullOrWhiteSpace(folderPath))
			folderPath = Environment.CurrentDirectory;

		return System.IO.Path.GetFullPath(folderPath);
	}

	private void ProcessDirectory(string folderPath) {
		DirectoryInfo dir = new(folderPath);
		IEnumerable<string> inkFiles = dir.GetFiles(_localizerOptions.FilePattern, SearchOption.AllDirectories)
			.Select(file => file.FullName);

		ProcessFiles(inkFiles);
		InsertTagsToFiles();
	}

	private void ProcessFiles(IEnumerable<string> inkFiles) {
		foreach (string inkFile in inkFiles) {
			string? content = FileHandler.LoadInkFileContents(inkFile);
			if (content == null)
				throw new InkParsingException($"Failed to load ink file \"{inkFile}\".");

			InkParser parser = new(content, inkFile, OnError, FileHandler);
			Story? story = parser.Parse();

			ProcessStory(story);
		}
	}

	private static void OnError(string message, ErrorType type) {
		throw new Exception("Ink Parse Error: " + message);
	}

	private void ProcessStory(Story story) {
		HashSet<string> newFilesVisited = [];

		List<Text> validTextObjects = FindLocalizableText(story, newFilesVisited);

		if (newFilesVisited.Count > 0)
			_filesVisited.UnionWith(newFilesVisited);

		_idGenerator.SetExistingIDs(validTextObjects);

		Strings.AddRange(_idGenerator.GenerateLocalizationIDs(validTextObjects));
	}

	private List<Text> FindLocalizableText(Story story, HashSet<string> newFilesVisited) {
		List<Text> validTextObjects = [];
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
				throw new InkParsingException(
					$"Error in file \"{fileId}\" line \"{lastLineNumber}\" - two chunks of text when localizer can only work with one per line.");
			}
			lastLineNumber = text.debugMetadata.startLineNumber;

			validTextObjects.Add(text);
		}
		return validTextObjects;
	}

	private static bool IsTextValid(Text text) {
		if (text.text.Trim() == string.Empty)
			return false;
		if (IsTextTag(text))
			return false;
		if (IsTextInsideCode(text))
			return false;

		return true;
	}

	// Go through every Ink file that needs a tag insertion, and insert!
	private void InsertTagsToFiles() {
		foreach ((string fileName, List<TagInsert> workList) in _idGenerator.FilesTagsToInsert) {
			if (workList.Count == 0)
				continue;

			Console.WriteLine($"Updating IDs in file: {fileName}");
			InsertTagsToFile(fileName, workList, FileHandler);
		}
	}
}