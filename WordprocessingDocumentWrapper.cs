using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace SabbathSchoolLessonBuilder;

public class WordprocessingDocumentWrapper : IDisposable
{
    private WordprocessingDocument? _document;
    private readonly string? _outputPath;

    private WordprocessingDocumentWrapper(WordprocessingDocument document, string? outputPath)
    {
        _document = document;
        _outputPath = outputPath;
    }

    public static WordprocessingDocumentWrapper CreateFromTemplate(string outputPath)
    {
        // Copy template to output location
        File.Copy("Template.docx", outputPath, overwrite: true);

        // Open the copied file for modification
        var doc = WordprocessingDocument.Open(outputPath, true);
        return new WordprocessingDocumentWrapper(doc, outputPath);
    }

    public Body Body => _document?.MainDocumentPart?.Document?.Body ?? throw new InvalidOperationException("Document is not initialized");

    public WordprocessingDocumentWrapper AddParagraph(string style, string text)
    {
        var para = new Paragraph();
        var pPr = new ParagraphProperties();
        pPr.Append(new ParagraphStyleId { Val = style });
        para.Append(pPr);

        var run = new Run();
        var t = new Text { Text = text };
        run.Append(t);
        para.Append(run);

        Body.Append(para);
        return this;
    }

    public Paragraph CreateParagraph()
    {
        var para = new Paragraph();
        Body.Append(para);
        return para;
    }

    public void ClearBody()
    {
        // Preserve SectionProperties (contains page size, margins, etc.)
        var sectionProperties = Body.Elements<SectionProperties>().FirstOrDefault();

        var elementsToRemove = Body.ChildElements.ToList();
        foreach (var element in elementsToRemove)
        {
            element.Remove();
        }

        // Re-add section properties to preserve template formatting
        if (sectionProperties != null)
        {
            Body.Append(sectionProperties);
        }
    }

    public WordprocessingDocument GetDocument() => _document ?? throw new InvalidOperationException("Document is disposed");

    public void Close()
    {
        _document?.Dispose();
        _document = null;
    }

    public void Dispose()
    {
        Close();
    }
}
