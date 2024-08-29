using Ink.Parsed;
using Object = Ink.Parsed.Object;

namespace InkLocalizer;

internal class IdGenerator(LocalizerOptions localizerOptions) {
	private readonly HashSet<string> _existingIDs = [];
	private static readonly Random Random = new();

	public readonly Dictionary<string, List<TagInsert>> FilesTagsToInsert = new();

	public void SetExistingIDs(List<Text> validTextObjects) {
		if (localizerOptions.ReTag)
			return;

		foreach (string? locTag in validTextObjects.Select(TagManagement.FindLocTagId).OfType<string>())
			_existingIDs.Add(locTag);
	}

	private static string MakeLocPrefix(Text text) {
		string prefix = string.Empty;
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
		string locId;
		do locId = locPrefix + GenerateId();
		while (!_existingIDs.Add(locId));

		return locId;
	}

	private string GenerateId(int length = 4) {
		const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
		char[] stringChars = new char[length];
		for (int i = 0; i < length; i++) {
			stringChars[i] = chars[Random.Next(chars.Length)];
		}
		return new string(stringChars);
	}

	private string GenerateFileId(string fileName, Text text) {
		string locPrefix = $"{System.IO.Path.GetFileNameWithoutExtension(fileName)}_{MakeLocPrefix(text)}";
		return GenerateUniqueId(locPrefix);
	}

	public Dictionary<string, string> GenerateLocalizationIDs(List<Text> validTextObjects) {
		Dictionary<string, string> strings = new();
		foreach (Text text in validTextObjects) {
			if (AlreadyTagged(text, strings))
				continue;

			string fileName = text.debugMetadata.fileName;
			string locId = GenerateFileId(System.IO.Path.GetFileNameWithoutExtension(fileName), text);

			// Add the ID and text object to a list of things to fix up in this file.
			if (!FilesTagsToInsert.ContainsKey(fileName))
				FilesTagsToInsert[fileName] = [];
			TagInsert insert = new() {
				Text = text,
				LocId = locId
			};
			FilesTagsToInsert[fileName].Add(insert);

			// Add new string to localization strings.
			AddString(locId, text.text, strings);
		}

		return strings;
	}

	private bool AlreadyTagged(Text text, Dictionary<string, string> strings) {
		string? locId = TagManagement.FindLocTagId(text);
		if (locId == null || localizerOptions.ReTag)
			return false;

		AddString(locId, text.text, strings);
		return true;
	}

	private void AddString(string locId, string value, Dictionary<string, string> strings) {
		if (strings.TryAdd(locId, value.Trim()))
			return;

		Console.Error.WriteLine(
			$"Unexpected behaviour - trying to add content for a string named {locId}, but one already exists? Have you duplicated a tag?");
	}
}