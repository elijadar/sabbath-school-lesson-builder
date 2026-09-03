using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace SabbathSchoolLessonBuilder;

public static class DocumentExtensions
{
    public static Body AddParagraph(this Body body, string style, string text)
    {
        var para = new Paragraph();
        var pPr = new ParagraphProperties();
        pPr.Append(new ParagraphStyleId { Val = style });
        para.Append(pPr);

        var run = new Run();
        var t = new Text { Text = text };
        run.Append(t);
        para.Append(run);

        body.Append(para);
        return body;
    }

    public static Paragraph WithStyle(this Paragraph para, string style)
    {
        var pPr = para.ParagraphProperties ?? new ParagraphProperties();
        pPr.ParagraphStyleId = new ParagraphStyleId { Val = style };
        if (para.ParagraphProperties == null)
        {
            para.PrependChild(pPr);
        }
        return para;
    }

    public static Paragraph WithText(this Paragraph para, string text)
    {
        var run = new Run();
        var t = new Text { Text = text };
        run.Append(t);
        para.Append(run);
        return para;
    }

    public static Run WithColor(this Run run, string color)
    {
        var rPr = run.RunProperties ?? new RunProperties();
        rPr.Color = new Color { Val = color };
        if (run.RunProperties == null)
        {
            run.PrependChild(rPr);
        }
        return run;
    }

    public static Run WithUnderline(this Run run)
    {
        var rPr = run.RunProperties ?? new RunProperties();
        rPr.Underline = new Underline { Val = UnderlineValues.Single };
        if (run.RunProperties == null)
        {
            run.PrependChild(rPr);
        }
        return run;
    }

    public static Run WithBreak(this Run run, BreakType type = BreakType.TextWrapping)
    {
        var br = new Break();
        if (type == BreakType.PageBreak)
        {
            br.Type = BreakValues.Page;
        }
        run.Append(br);
        return run;
    }

    public static Run WithAppendedText(this Run run, string text)
    {
        run.Append(new Text { Text = text });
        return run;
    }

    public static TOut Pipe<T, TOut>(this T input, Func<T, TOut> func) => func(input);

    public static TOut Pipe<T, TA, TOut>(this T input, Func<T, TA, TOut> func, TA arg) => func(input, arg);
}

public enum BreakType
{
    TextWrapping,
    PageBreak
}
