using SabbathSchoolLessonBuilder;

namespace SabbathSchoolLessonBuilder.Tests
{
    public class TextCleanupTests
    {
        [Fact]
        public void ExtractVerseReferences_SingleReference_ReturnsIt()
        {
            var result = TextCleanup.ExtractVerseReferences("Ів. 3:16");

            var reference = Assert.Single(result);
            Assert.Equal("Ів. 3:16", reference);
        }

        [Fact]
        public void ExtractVerseReferences_MultipleReferencesSeparatedByText_ReturnsBoth()
        {
            var result = TextCleanup.ExtractVerseReferences("Ів. 3:16; Рим. 5:8");

            Assert.Equal(2, result.Count);
            Assert.Equal("Ів. 3:16", result[0]);
            Assert.Equal("Рим. 5:8", result[1]);
        }

        [Fact]
        public void ExtractVerseReferences_EnDashInVerseRange_IsNormalizedToHyphen()
        {
            var result = TextCleanup.ExtractVerseReferences("Об’явл. 12:7–8");

            var reference = Assert.Single(result);
            Assert.Equal("Об’явл. 12:7-8", reference);
        }

        [Fact]
        public void ExtractVerseReferences_TrailingPunctuation_IsStripped()
        {
            var result = TextCleanup.ExtractVerseReferences("Ів. 3:16,");

            var reference = Assert.Single(result);
            Assert.Equal("Ів. 3:16", reference);
        }

        [Fact]
        public void ExtractVerseReferences_LeadingSeparatorPunctuation_IsStripped()
        {
            var result = TextCleanup.ExtractVerseReferences(": Ів. 3:16");

            var reference = Assert.Single(result);
            Assert.Equal("Ів. 3:16", reference);
        }

        [Fact]
        public void ExtractVerseReferences_NoReferencePresent_ReturnsEmpty()
        {
            var result = TextCleanup.ExtractVerseReferences("немає посилання тут");

            Assert.Empty(result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ExtractVerseReferences_NullOrEmptyInput_ReturnsEmpty(string? input)
        {
            var result = TextCleanup.ExtractVerseReferences(input!);

            Assert.Empty(result);
        }

        [Fact]
        public void TrimQuestionText_LeadingNumber_IsRemoved()
        {
            var result = TextCleanup.TrimQuestionText("1. Що це означає?");

            Assert.Equal("Що це означає?", result);
        }

        [Theory]
        [InlineData("Прочитайте тексти Ів. 3:16.", "Ів. 3:16.")]
        [InlineData("Перегляньте уривок Ів. 3:16.", "Ів. 3:16.")]
        [InlineData("Прочитайте уривок Ів. 3:16.", "Ів. 3:16.")]
        [InlineData("Прочитайте Ів. 3:16.", "Ів. 3:16.")]
        public void TrimQuestionText_KnownLeadInPhrases_AreStripped(string input, string expected)
        {
            var result = TextCleanup.TrimQuestionText(input);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void TrimQuestionText_DivAbbreviation_IsStripped()
        {
            var result = TextCleanup.TrimQuestionText("див. Ів. 3:16");

            Assert.Equal("Ів. 3:16", result);
        }

        [Fact]
        public void TrimQuestionText_LeadingTextWord_IsStripped()
        {
            var result = TextCleanup.TrimQuestionText("текст Ів. 3:16");

            Assert.Equal("Ів. 3:16", result);
        }

        [Fact]
        public void TrimQuestionText_TrimsSurroundingWhitespace()
        {
            var result = TextCleanup.TrimQuestionText("  Що це означає?  ");

            Assert.Equal("Що це означає?", result);
        }

        [Fact]
        public void CleanupAfterVerseRemoval_EmptyParenthesesLeftBehind_AreRemoved()
        {
            // The trailing "." right after the empty "(Прочитайте )" is consumed too
            // (the `\.*` in the pattern is deliberately greedy about trailing dots).
            var result = TextCleanup.CleanupAfterVerseRemoval("Прочитайте текст (Прочитайте ). Що це означає?");

            Assert.Equal("Прочитайте текст Що це означає?", result);
        }

        [Fact]
        public void CleanupAfterVerseRemoval_LeadingConnectorTa_IsRemoved()
        {
            var result = TextCleanup.CleanupAfterVerseRemoval("та решта тексту");

            Assert.Equal("решта тексту", result);
        }

        [Fact]
        public void CleanupAfterVerseRemoval_LeadingConnectorI_IsRemoved()
        {
            var result = TextCleanup.CleanupAfterVerseRemoval("і решта тексту");

            Assert.Equal("решта тексту", result);
        }

        [Fact]
        public void CleanupAfterVerseRemoval_LeadingSeparatorPunctuation_IsRemoved()
        {
            var result = TextCleanup.CleanupAfterVerseRemoval(": решта тексту");

            Assert.Equal("решта тексту", result);
        }

        [Theory]
        [InlineData("Филм", "Филимона")]
        [InlineData("Филм.", "Филимона")]
        [InlineData("филм", "Филимона")]
        [InlineData("Мих", "Міхея")]
        [InlineData("Мих.", "Міхея")]
        public void NormalizeBookNameOverride_KnownAbbreviations_AreExpanded(string input, string expected)
        {
            var result = TextCleanup.NormalizeBookNameOverride(input);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void NormalizeBookNameOverride_UnknownBookName_IsReturnedUnchanged()
        {
            var result = TextCleanup.NormalizeBookNameOverride("Ів");

            Assert.Equal("Ів", result);
        }

        [Fact]
        public void NormalizeBookNameOverride_EmptyInput_ReturnsEmpty()
        {
            var result = TextCleanup.NormalizeBookNameOverride(string.Empty);

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void ParseMemoryVerse_WellFormedVerse_ReturnsReferenceAndText()
        {
            // Pre-existing quirk carried over from the original GetVerse() logic: the
            // text is cut one character short of the closing "»" (off-by-one in the
            // original substring length). Preserved as-is since fixing behavior isn't
            // this extraction's goal; documented here so it's visible/testable.
            var (reference, text) = TextCleanup.ParseMemoryVerse("«Бо так Бог полюбив світ» (Ів. 3:16)");

            Assert.Equal("Ів. 3:16", reference);
            Assert.Equal("Бо так Бог полюбив сві", text);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void ParseMemoryVerse_NullOrBlankInput_ReturnsEmptyPair(string? input)
        {
            var (reference, text) = TextCleanup.ParseMemoryVerse(input!);

            Assert.Equal(string.Empty, reference);
            Assert.Equal(string.Empty, text);
        }

        [Fact]
        public void ParseMemoryVerse_MissingParentheses_ReturnsEmptyPair()
        {
            var (reference, text) = TextCleanup.ParseMemoryVerse("«Бо так Бог полюбив світ» Ів. 3:16");

            Assert.Equal(string.Empty, reference);
            Assert.Equal(string.Empty, text);
        }

        [Fact]
        public void ParseMemoryVerse_MissingClosingGuillemet_ReturnsEmptyPair()
        {
            var (reference, text) = TextCleanup.ParseMemoryVerse("«Бо так Бог полюбив світ (Ів. 3:16)");

            Assert.Equal(string.Empty, reference);
            Assert.Equal(string.Empty, text);
        }

        [Fact]
        public void ParseMemoryVerse_ClosingParenBeforeOpening_ReturnsEmptyPair()
        {
            var (reference, text) = TextCleanup.ParseMemoryVerse("«text» )Ів. 3:16(");

            Assert.Equal(string.Empty, reference);
            Assert.Equal(string.Empty, text);
        }
    }
}
