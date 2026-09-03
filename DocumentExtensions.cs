using NPOI.XWPF.UserModel;

namespace SabbathSchoolLessonBuilder;

public static class DocumentExtensions
{
    public static XWPFDocument AddParagraph(this XWPFDocument doc, string style, string text)
    {
        doc.CreateParagraph()
            .WithStyle(style)
            .WithText(text);
        return doc;
    }

    public static XWPFParagraph WithStyle(this XWPFParagraph para, string style)
    {
        para.Style = style;
        return para;
    }

    public static XWPFParagraph WithText(this XWPFParagraph para, string text)
    {
        para.CreateRun().SetText(text);
        return para;
    }

    public static XWPFRun WithColor(this XWPFRun run, string color)
    {
        run.SetColor(color);
        return run;
    }

    public static XWPFRun WithUnderline(this XWPFRun run)
    {
        run.Underline = UnderlinePatterns.Single;
        return run;
    }

    public static XWPFRun WithBreak(this XWPFRun run, BreakType type = BreakType.TEXTWRAPPING)
    {
        run.AddBreak(type);
        return run;
    }

    public static XWPFRun WithAppendedText(this XWPFRun run, string text)
    {
        run.AppendText(text);
        return run;
    }

    public static TOut Pipe<T, TOut>(this T input, Func<T, TOut> func) => func(input);

    public static TOut Pipe<T, TA, TOut>(this T input, Func<T, TA, TOut> func, TA arg) => func(input, arg);
}