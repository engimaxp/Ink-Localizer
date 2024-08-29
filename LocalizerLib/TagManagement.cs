using System.Text;
using System.Text.RegularExpressions;
using Ink;
using Ink.Parsed;
using Object = Ink.Parsed.Object;

namespace InkLocalizer;

internal static partial class TagManagement {
	private const string TagLoc = "id:";
	private const bool DebugReTagFiles = false;

	public static bool TryInsertTagsToFile(string fileName, List<TagInsert> workList, IFileHandler fileHandler) {
		try {
			string filePath = fileHandler.ResolveInkFilename(fileName);
			string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);

			foreach (TagInsert item in workList) {
				// Find out where we're supposed to do the insert.
				int lineNumber = item.Text.debugMetadata.endLineNumber - 1;
				string newLine = InsertTagInLine(item, lines, lineNumber);

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

	[GeneratedRegex($@"(#{TagLoc})\w+")]
	private static partial Regex TagRegex();
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
}