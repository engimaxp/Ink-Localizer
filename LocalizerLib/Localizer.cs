using Ink;
using Ink.Parsed;
using Object = Ink.Parsed.Object;

namespace InkLocalizer;

public sealed class Localizer(Localizer.Options? options = null) {
	public class Options {
		// If true, re-tag everything.
		public bool ReTag = false;

		// Root folder. If empty, uses current working dir.
		public string Folder = "";

		// Files to include. Will search subfolders of the working dir.
		public string FilePattern = "*.ink";
	}

	private readonly Options _options = options ?? new Options();

	private static readonly IFileHandler FileHandler = new DefaultFileHandler();
	private bool _inkParseErrors;
	private readonly HashSet<string> _filesVisited = [];
	private readonly Dictionary<string, List<TagInsert>> _filesTagsToInsert = new();
	private readonly HashSet<string> _existingIDs = [];

	private readonly List<string> _stringKeys = [];
	public IList<string> StringKeys => _stringKeys;
	private readonly Dictionary<string, string> _stringValues = new();
	private string _previousCwd = "";

	// Return the text of a string, by locID
	public string GetString(string locId) => _stringValues[locId];

	public bool Run() {
		// ----- Figure out which files to include -----
		List<string> inkFiles = [];

		string folderPath = GetDirectoryPath();
		// Need this for InkParser to work properly with includes and such.
		Directory.SetCurrentDirectory(folderPath);
		bool success = TryProcessDirectory(folderPath, inkFiles);
		Directory.SetCurrentDirectory(_previousCwd);

		return success;
	}

	private string GetDirectoryPath() {
		_previousCwd = Environment.CurrentDirectory;

		string folderPath = _options.Folder;
		if (string.IsNullOrWhiteSpace(folderPath))
			folderPath = _previousCwd;
		folderPath = System.IO.Path.GetFullPath(folderPath);
		return folderPath;
	}

	private bool TryProcessDirectory(string folderPath, List<string> inkFiles) {
		try {
			DirectoryInfo dir = new(folderPath);
			inkFiles.AddRange(dir.GetFiles(_options.FilePattern, SearchOption.AllDirectories)
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
			if (content == null) {
				return false;
			}

			InkParser parser = new(content, inkFile, OnError, FileHandler);

			Story? story = parser.Parse();
			if (_inkParseErrors) {
				Console.Error.WriteLine("Error parsing ink file.");
				return false;
			}

			// Go through the parsed story extracting existing localised lines, and lines still to be localised...
			if (!ProcessStory(story)) {
				return false;
			}
		}
		return true;
	}

	private bool ProcessStory(Story story) {
		HashSet<string> newFilesVisited = [];

		if (!FindLocalizableText(story, newFilesVisited, out List<Text> validTextObjects))
			return false;

		if (newFilesVisited.Count > 0)
			_filesVisited.UnionWith(newFilesVisited);

		GetExistingIDs(validTextObjects);

		// IDs are stored as tags in the form #id:file_knot_stitch_xxxx
		GenerateLocalizationIDs(validTextObjects);

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
		if (text.text.Trim() == "")
			return false;

		// If it's a tag, ignore.
		if (TagManagement.IsTextTag(text))
			return false;

		// Is this inside some code? In which case we can't do anything with that.
		if (text.parent is VariableAssignment or StringExpression)
			return false;

		return true;
	}

	private void GetExistingIDs(List<Text> validTextObjects) {
		if (_options.ReTag)
			return;

		foreach (string? locTag in validTextObjects.Select(TagManagement.FindLocTagId).OfType<string>())
			_existingIDs.Add(locTag);
	}

	private void GenerateLocalizationIDs(List<Text> validTextObjects) {
		foreach (Text text in validTextObjects) {
			// Does the source already have a #id: tag?
			string? locId = TagManagement.FindLocTagId(text);

			// Skip if there's a tag and we aren't forcing a re-tag
			if (locId != null && !_options.ReTag) {
				// Add existing string to localisation strings.
				AddString(locId, text.text);
				continue;
			}

			// Generate a new ID
			string fileName = text.debugMetadata.fileName;
			string fileId = System.IO.Path.GetFileNameWithoutExtension(fileName);
			string pathPrefix = fileId + "_";
			string locPrefix = pathPrefix + MakeLocPrefix(text);
			locId = GenerateUniqueId(locPrefix);

			// Add the ID and text object to a list of things to fix up in this file.
			if (!_filesTagsToInsert.ContainsKey(fileName))
				_filesTagsToInsert[fileName] = [];
			TagInsert insert = new() {
				Text = text,
				LocId = locId
			};
			_filesTagsToInsert[fileName].Add(insert);

			// Add new string to localisation strings.
			AddString(locId, text.text);
		}
	}

	private void AddString(string locId, string value) {
		if (_stringKeys.Contains(locId)) {
			Console.Error.WriteLine(
				$"Unexpected behaviour - trying to add content for a string named {locId}, but one already exists? Have you duplicated a tag?");
			return;
		}

		// Keeping the order of strings.
		_stringKeys.Add(locId);
		_stringValues[locId] = value.Trim();
	}

	// Go through every Ink file that needs a tag insertion, and insert!
	private bool InsertTagsToFiles() {
		foreach ((string fileName, List<TagInsert> workList) in _filesTagsToInsert) {
			if (workList.Count == 0)
				continue;

			Console.WriteLine($"Updating IDs in file: {fileName}");

			if (!TagManagement.InsertTagsToFile(fileName, workList))
				return false;
		}
		return true;
	}

	// Constructs a prefix from knot / stitch
	private static string MakeLocPrefix(Text text) {
		string prefix = "";
		foreach (Object? obj in text.ancestry) {
			switch (obj) {
				case Knot knot:
					prefix += $"{knot.name}_";
					break;
				case Stitch stitch:
					prefix += $"{stitch.name}_";
					break;
			}
		}

		return prefix;
	}

	private string GenerateUniqueId(string locPrefix) {
		// Repeat a lot to try and get options. Should be hard to fail at this but
		// let's set a limit to stop locks.
		for (int i = 0; i < 100; i++) {
			string locId = locPrefix + GenerateId();
			if (_existingIDs.Add(locId)) {
				return locId;
			}
		}
		throw new Exception("Couldn't generate a unique ID! Really unlikely. Try again!");
	}

	private static readonly Random Random = new();

	private static string GenerateId(int length = 4) {
		const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
		char[] stringChars = new char[length];
		for (int i = 0; i < length; i++) {
			stringChars[i] = chars[Random.Next(chars.Length)];
		}
		return new string(stringChars);
	}

	private void OnError(string message, ErrorType type) {
		_inkParseErrors = true;
		Console.Error.WriteLine("Ink Parse Error: " + message);
	}
}