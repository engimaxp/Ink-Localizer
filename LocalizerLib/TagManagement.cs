using System.Text;
using System.Text.RegularExpressions;
using Ink;
using Ink.Parsed;
using Object = Ink.Parsed.Object;

namespace InkLocalizer;

internal static partial class TagManagement {
	private const string TagLoc = "id:";
	private const bool DebugReTagFiles = true;

	[GeneratedRegex($@"(#{TagLoc})\w+")]
	private static partial Regex TagRegex();

	public static void InsertTagsToFile(string fileName, List<TagInsert> workList, IFileHandler fileHandler) {
		string filePath = fileHandler.ResolveInkFilename(fileName);
		string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
		Console.WriteLine(filePath);

		foreach (TagInsert item in workList) {
			int lineNumber = item.Text.debugMetadata.endLineNumber - 1;
			string newLine = InsertTagInLine(item, lines, lineNumber);

			lines[lineNumber] = newLine;
		}

		string output = string.Join("\n", lines);
		string outputFilePath = filePath;
		if (DebugReTagFiles)
			outputFilePath += ".txt";

		Console.WriteLine(outputFilePath);
		File.WriteAllText(outputFilePath, output, Encoding.UTF8);
	}

	private static string InsertTagInLine(TagInsert item, string[] lines, int lineNumber) {
		string newTag = $"#{TagLoc}{item.LocId}";
		string oldLine = lines[lineNumber];

		if (oldLine.Contains($"#{TagLoc}")) {
			// Is there already a tag called #id: in there? In which case, we just want to replace that.
			return TagRegex().Replace(oldLine, $"{newTag}");
		}
		// No tag, add a new one.
		int charPos = item.Text.debugMetadata.endCharacterNumber - 1;

		// Pad between other tags or previous item
		if (!char.IsWhiteSpace(oldLine[charPos - 1]))
			newTag = $" {newTag}";
		if (oldLine.Length > charPos && (oldLine[charPos] == '#' || oldLine[charPos] == '/'))
			newTag += " ";

		return oldLine.Insert(charPos, newTag);
	}

	public static string? FindLocTagId(Text text) {
		List<string> tags = GetTagsAfterText(text);
		return tags.Count > 0
			? (from tag in tags
				where tag.StartsWith(TagLoc)
				select tag[TagLoc.Length..]).FirstOrDefault()
			: null;
	}

	private static List<string> GetTagsAfterText(Text text) {
		List<string> tags = [];

		bool afterText = false;
		int inTag = 0;

		foreach (Object sibling in text.parent.content) {
			if (IsBeforeText(text, sibling, ref afterText))
				continue;

			if (IsEndOfLine(sibling))
				return tags;

			if (IsTag(sibling, ref inTag))
				continue;

			AddTag(inTag, sibling, tags);
		}
		return tags;
	}

	private static bool IsBeforeText(Text text, Object sibling, ref bool afterText) {
		if (sibling == text) {
			afterText = true;
			return true;
		}
		if (!afterText)
			return true;
		return false;
	}

	private static bool IsEndOfLine(Object sibling) {
		return sibling is Text { text: "\n" };
	}

	private static void AddTag(int inTag, Object sibling, List<string> tags) {
		if (inTag > 0 && sibling is Text text) {
			tags.Add(text.text.Trim());
		}
	}

	private static bool IsTag(Object sibling, ref int inTag) {
		if (sibling is not Tag tag)
			return false;

		if (tag.isStart)
			inTag++;
		else
			inTag--;
		return true;
	}

	// Checking it's a tag. Is there a StartTag earlier in the parent content?
	public static bool IsTextTag(Text text) {
		int inTag = 0;
		foreach (Object? sibling in text.parent.content.TakeWhile(sibling => sibling != text)) {
			if (sibling is not Tag tag)
				continue;

			if (tag.isStart)
				inTag++;
			else
				inTag--;
		}

		return inTag > 0;
	}
	public static bool IsTextInsideCode(Text text) =>
		text.parent is VariableAssignment or StringExpression;
}