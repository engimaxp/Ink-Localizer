using System.Text;
using System.Text.RegularExpressions;
using Ink;
using Ink.Parsed;
using Object = Ink.Parsed.Object;

namespace InkLocalizer;

public sealed class Localizer(Localizer.Options? options = null) {
	private const string TagLoc = "id:";
	private const bool DebugReTagFiles = false;

	public class Options {
		// If true, re-tag everything.
		public bool ReTag = false;

		// Root folder. If empty, uses current working dir.
		public string Folder = "";

		// Files to include. Will search subfolders of the working dir.
		public string FilePattern = "*.ink";
	}

	private readonly Options _options = options ?? new Options();

	private struct TagInsert {
		public Text Text;
		public string LocId;
	}

	private readonly IFileHandler _fileHandler = new DefaultFileHandler();
	private bool _inkParseErrors;
	private readonly HashSet<string> _filesVisited = [];
	private readonly Dictionary<string, List<TagInsert>> _filesTagsToInsert = new();
	private readonly HashSet<string> _existingIDs = [];

	private readonly List<string> _stringKeys = [];
	private readonly Dictionary<string, string> _stringValues = new();
	private string _previousCwd = "";

	public bool Run() {
		bool success = true;

		// ----- Figure out which files to include -----
		List<string> inkFiles = [];

		// We'll restore this later.
		_previousCwd = Environment.CurrentDirectory;

		string folderPath = _options.Folder;
		if (string.IsNullOrWhiteSpace(folderPath))
			folderPath = _previousCwd;
		folderPath = System.IO.Path.GetFullPath(folderPath);

		// Need this for InkParser to work properly with includes and such.
		Directory.SetCurrentDirectory(folderPath);

		try {
			DirectoryInfo dir = new(folderPath);
			inkFiles.AddRange(dir.GetFiles(_options.FilePattern, SearchOption.AllDirectories).Select(file => file.FullName));
		}
		catch (Exception ex) {
			Console.Error.WriteLine($"Error finding files to process: {folderPath}: " + ex.Message);
			success = false;
		}

		// ----- For each file... -----
		if (success) {
			foreach (string inkFile in inkFiles) {
				string? content = _fileHandler.LoadInkFileContents(inkFile);
				if (content == null) {
					success = false;
					break;
				}

				InkParser parser = new(content, inkFile, OnError, _fileHandler);

				Story? story = parser.Parse();
				if (_inkParseErrors) {
					Console.Error.WriteLine($"Error parsing ink file.");
					success = false;
					break;
				}

				// Go through the parsed story extracting existing localised lines, and lines still to be localised...
				if (!ProcessStory(story)) {
					success = false;
					break;
				}
			}
		}

		// If new tags need to be added, add them now.
		if (success) {
			if (!InsertTagsToFiles()) {
				success = false;
			}
		}

		// Restore current directory.
		Directory.SetCurrentDirectory(_previousCwd);

		return success;
	}

	// List all the locIDs for every string we found, in order.
	public IList<string> GetStringKeys() {
		return _stringKeys;
	}

	// Return the text of a string, by locID
	public string GetString(string locId) {
		return _stringValues[locId];
	}

	private bool ProcessStory(Story story) {
		HashSet<string> newFilesVisited = [];

		// ---- Find all the things we should localise ----
		List<Text> validTextObjects = [];
		int lastLineNumber = -1;
		foreach (Text? text in story.FindAll<Text>()) {
			// Just a newline? Ignore.
			if (text.text.Trim() == "")
				continue;

			// If it's a tag, ignore.
			if (IsTextTag(text))
				continue;

			// Is this inside some code? In which case we can't do anything with that.
			if (text.parent is VariableAssignment ||
			    text.parent is StringExpression) {
				continue;
			}

			// Have we already visited this source file i.e. is it in an include we've seen before?
			// If so, skip.
			string fileId = System.IO.Path.GetFileNameWithoutExtension(text.debugMetadata.fileName);
			if (_filesVisited.Contains(fileId)) {
				continue;
			}
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

		if (newFilesVisited.Count > 0)
			_filesVisited.UnionWith(newFilesVisited);

		// ---- Scan for existing IDs ----
		// Build list of existing IDs (so we don't duplicate)
		if (!_options.ReTag) {
			// Don't do this if we want to retag everything.
			foreach (string? locTag in validTextObjects.Select(FindLocTagId).OfType<string>()) {
				_existingIDs.Add(locTag);
			}
		}

		// ---- Sort out IDs ----
		// Now we've got our list of text, let's iterate through looking for IDs, and create them when they're missing.
		// IDs are stored as tags in the form #id:file_knot_stitch_xxxx

		foreach (Text text in validTextObjects) {
			// Does the source already have a #id: tag?
			string? locId = FindLocTagId(text);

			// Skip if there's a tag and we aren't forcing a retag
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

		return true;
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

			if (!InsertTagsToFile(fileName, workList))
				return false;
		}
		return true;
	}

	// Do the tag inserts for one specific file.
	private bool InsertTagsToFile(string fileName, List<TagInsert> workList) {
		try {
			string filePath = _fileHandler.ResolveInkFilename(fileName);
			string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);

			foreach (TagInsert item in workList) {
				// Tag
				string newTag = $"#{TagLoc}{item.LocId}";

				// Find out where we're supposed to do the insert.
				int lineNumber = item.Text.debugMetadata.endLineNumber - 1;
				string oldLine = lines[lineNumber];
				string newLine = "";

				if (oldLine.Contains($"#{TagLoc}")) {
					// Is there already a tag called #id: in there? In which case, we just want to replace that.

					// Regex pattern to find "#id:" followed by any alphanumeric characters or underscores
					string pattern = $@"(#{TagLoc})\w+";

					// Replace the matched text
					newLine = Regex.Replace(oldLine, pattern, $"{newTag}");
				} else {
					// No tag, add a new one.
					int charPos = item.Text.debugMetadata.endCharacterNumber - 1;

					// Pad between other tags or previous item
					if (!Char.IsWhiteSpace(oldLine[charPos - 1]))
						newTag = " " + newTag;
					if (oldLine.Length > charPos && (oldLine[charPos] == '#' || oldLine[charPos] == '/'))
						newTag = newTag + " ";

					newLine = oldLine.Insert(charPos, newTag);
				}

				lines[lineNumber] = newLine;
			}

			// Write out to the input file.
			string output = string.Join("\n", lines);
			string outputFilePath = filePath;
			if (DebugReTagFiles) // Debug purposes, copy to a different file instead.
				outputFilePath += ".txt";
			File.WriteAllText(outputFilePath, output, Encoding.UTF8);
			return true;
		}
		catch (Exception ex) {
			Console.Error.WriteLine($"Error replacing tags in {fileName}: " + ex.Message);
			return false;
		}
	}

	// Checking it's a tag. Is there a StartTag earlier in the parent content?
	private static bool IsTextTag(Text text) {
		int inTag = 0;
		foreach (var sibling in text.parent.content) {
			if (sibling == text)
				break;
			if (sibling is Tag) {
				var tag = (Tag)sibling;
				if (tag.isStart)
					inTag++;
				else
					inTag--;
			}
		}

		return (inTag > 0);
	}

	private static string? FindLocTagId(Text text) {
		List<string> tags = GetTagsAfterText(text);
		return tags.Count > 0
			? (from tag in tags where tag.StartsWith(TagLoc)
				select tag[TagLoc.Length..]).FirstOrDefault()
			: null;
	}

	private static List<string> GetTagsAfterText(Text text) {
		List<string> tags = [];

		bool afterText = false;
		int inTag = 0;

		foreach (Object? sibling in text.parent.content) {
			// Have we hit the text we care about yet? If not, carry on.
			if (sibling == text) {
				afterText = true;
				continue;
			}
			if (!afterText)
				continue;

			// Have we hit an end-of-line marker? If so, stop looking, no tags here.
			if (sibling is Text { text: "\n" })
				break;

			// Have we found the start or end of a tag?
			if (sibling is Tag tag) {
				if (tag.isStart)
					inTag++;
				else
					inTag--;
				continue;
			}

			// Have we hit the end of a tag? Add it to our tag list!
			if (inTag > 0 && sibling is Text text1) {
				tags.Add(text1.text.Trim());
			}
		}
		return tags;
	}

	// Constructs a prefix from knot / stitch
	private static string MakeLocPrefix(Text text) {
		string prefix = "";
		foreach (Object? obj in text.ancestry) {
			switch (obj) {
				case Knot knot:
					prefix += knot.name + "_";
					break;
				case Stitch stitch:
					prefix += stitch.name + "_";
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