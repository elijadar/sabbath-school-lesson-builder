using System.Text.RegularExpressions;

namespace SabbathSchoolLessonBuilder;

/// <summary>
/// Regex-heavy Ukrainian text-cleanup helpers used while scraping and building
/// lesson documents (see <see cref="WebScrapping"/>). Pulled out on its own so the
/// fiddly parsing/regex logic can be unit-tested independently of the HTTP/HTML
/// scraping and NPOI document generation it's normally embedded in.
/// </summary>
public static class TextCleanup
{
    private static readonly Regex VerseReferencePattern = new(
        @"([\dІ\.]{0,2}\s*[а-яА-ЯІїєюіЇЄЮ’]+\.{0,1}\s+\d+\:[\d,\-\s]+)(?:(?![\dІ\.]{0,2}\s*[а-яА-ЯІїєюіЇЄЮ’]+\.{0,1}\s+\d+\:[\d,\-\s]+).)*",
        RegexOptions.Compiled);

    /// <summary>
    /// Extracts individual Bible verse references (e.g. "Ів. 3:16") out of the raw
    /// text of an `&lt;a class="verse"&gt;` link, which may contain several
    /// references separated by other text/punctuation.
    /// </summary>
    public static IList<string> ExtractVerseReferences(string rawVerseText)
    {
        var verses = new List<string>();
        if (string.IsNullOrEmpty(rawVerseText))
        {
            return verses;
        }

        var normalized = rawVerseText.Replace("–", "-");
        var matches = VerseReferencePattern.Matches(normalized);
        foreach (Match m in matches)
        {
            var value = Regex.Replace(m.Value.Trim(), @"^\s*[\.\:\;] ", "");
            value = Regex.Replace(value, @"\s*[\,\;\.]$", "");
            verses.Add(value);
        }

        return verses;
    }

    /// <summary>
    /// Trims a raw discussion-question string down to its actual question text by
    /// stripping the leading item number and known Ukrainian lead-in phrases
    /// ("Прочитайте ...", "Перегляньте уривок ...", "див.", "текст", etc.).
    /// </summary>
    public static string TrimQuestionText(string question)
    {
        var result = question.Trim();
        result = Regex.Replace(result, @"^\d+\. ", "");
        result = Regex.Replace(result, @"^Прочитайте\s+тексти\s*", "");
        result = Regex.Replace(result, @"^Перегляньте\s+уривок\s*", "");
        result = Regex.Replace(result, @"^Прочитайте\s+уривок\s*", "");
        result = Regex.Replace(result, @"^Прочитайте\s*", "");
        result = Regex.Replace(result, @"^\s*див.\s*", "");
        result = Regex.Replace(result, @"^\s*текст\s*", "");
        return result;
    }

    /// <summary>
    /// Cleans up the leftover question text after a verse reference has been cut
    /// out of it: drops empty "(Прочитайте )"/"(див. )" style remnants and leading
    /// connectors ("та ", "і ") or stray punctuation left behind by the removal.
    /// </summary>
    public static string CleanupAfterVerseRemoval(string text)
    {
        var result = text;
        result = Regex.Replace(result, @"\s*\((?:Прочитайте|див\.|Див\.\s+також)?\s*\)\.*", "");
        result = Regex.Replace(result, @"^\s*[\.\:\;] ", "");
        result = Regex.Replace(result, @"^\s*та ", "");
        result = Regex.Replace(result, @"^\s*і ", "");
        return result;
    }

    /// <summary>
    /// Applies known abbreviated-book-name overrides that the source API's short
    /// forms don't resolve against uniquely/correctly (e.g. "Филм." for Philemon
    /// would otherwise collide, and "Мих." needs expanding to match the full book
    /// name list).
    /// </summary>
    public static string NormalizeBookNameOverride(string bookName)
    {
        if (string.IsNullOrEmpty(bookName))
        {
            return bookName;
        }

        if (bookName.StartsWith("Филм", StringComparison.InvariantCultureIgnoreCase))
        {
            return "Филимона";
        }

        if (bookName.StartsWith("Мих", StringComparison.InvariantCultureIgnoreCase))
        {
            return "Міхея";
        }

        return bookName;
    }

    /// <summary>
    /// Parses the memory-verse blockquote text (e.g. "«...» (Ів. 3:16)") into a
    /// (reference, quoted text) pair. Returns a pair of empty strings when the
    /// text doesn't match the expected "«quote» (reference)" shape.
    /// </summary>
    /// <remarks>
    /// Behavior preserved from the original inline implementation: the returned
    /// text is one character short of the closing "»" (see
    /// <c>closingGuillemetIdx - 1</c> below) - a pre-existing off-by-one quirk,
    /// not something introduced by this extraction. Left as-is to keep this
    /// change a pure refactor; see the covering test for the exact behavior.
    /// </remarks>
    public static (string Reference, string Text) ParseMemoryVerse(string? verse)
    {
        var empty = string.Empty;
        if (string.IsNullOrWhiteSpace(verse) || verse.IndexOf('(') < 0)
        {
            return (empty, empty);
        }

        var openIdx = verse.IndexOf('(');
        var closeIdx = verse.IndexOf(')');
        if (closeIdx < 0 || closeIdx < openIdx)
        {
            return (empty, empty);
        }

        var reference = verse.Substring(openIdx + 1, closeIdx - openIdx - 1);

        var trimmed = verse.TrimStart('«');
        var closingGuillemetIdx = trimmed.IndexOf('»');
        if (closingGuillemetIdx < 1)
        {
            return (empty, empty);
        }

        var text = trimmed[..(closingGuillemetIdx - 1)];
        return (reference, text);
    }
}