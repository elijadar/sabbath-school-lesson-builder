using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using NPOI.XWPF.UserModel;
using Polly;
using Polly.Retry;

namespace SabbathSchoolLessonBuilder;

public class WebScrapping
{
    private const string BibleUrl = "https://www.bible.com/uk/bible/3786/";

    private static readonly TimeSpan RequestThrottleDelay = TimeSpan.FromMilliseconds(300);

    private static readonly ResiliencePipeline<HttpResponseMessage> RetryPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
        .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
        {
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>()
                .HandleResult(response => response.StatusCode == HttpStatusCode.RequestTimeout
                                          || response.StatusCode == HttpStatusCode.TooManyRequests
                                          || (int)response.StatusCode >= 500),
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromSeconds(1),
            UseJitter = true
        })
        .Build();

    private static readonly IReadOnlyList<(string Key, string Value)> BooksOfBible =
    [
        ("ПЕРША КНИГА МОЙСЕЯ: БУТТЯ", "GEN"),
        ("ДРУГА КНИГА МОЙСЕЯ: ВИХІД", "EXO"),
        ("ТРЕТЯ КНИГА МОЙСЕЯ: ЛЕВІТИ", "LEV"),
        ("ЧЕТВЕРТА КНИГА МОЙСЕЯ: ЧИСЛА", "NUM"),
        ("П'ЯТА КНИГА МОЙСЕЯ: ВТОРОЗАКОННЯ", "DEU"),
        ("КНИГА ІСУСА НАВИНА", "JOS"),
        ("КНИГА СУДДІВ", "JDG"),
        ("КНИГА РУТ", "RUT"),
        ("ПЕРША КНИГА САМУЇЛА", "1SA"),
        ("ДРУГА КНИГА САМУЇЛА", "2SA"),
        ("ПЕРША КНИГА ЦАРІВ", "1KI"),
        ("ДРУГА КНИГА ЦАРІВ", "2KI"),
        ("ПЕРША КНИГА ХРОНІК", "1CH"),
        ("ДРУГА КНИГА ХРОНІК", "2CH"),
        ("КНИГА ЕЗДРИ", "EZR"),
        ("КНИГА НЕЄМІЇ", "NEH"),
        ("КНИГА ЕСТЕР", "EST"),
        ("КНИГА ЙОВА", "JOB"),
        ("КНИГА ПСАЛМІВ", "PSA"),
        ("ПРИТЧІ СОЛОМОНА", "PRO"),
        ("КНИГА ЕКЛЕЗІАСТА", "ECC"),
        ("КНИГА ПІСНЯ ПІСЕНЬ", "SNG"),
        ("КНИГА ПРОРОКА ІСАЇ", "ISA"),
        ("КНИГА ПРОРОКА ЄРЕМІЇ", "JER"),
        ("КНИГА ПЛАЧ ЄРЕМІЇ", "LAM"),
        ("КНИГА ПРОРОКА ЄЗЕКІЇЛЯ", "EZK"),
        ("КНИГА ПРОРОКА ДАНИЇЛА", "DAN"),
        ("КНИГА ПРОРОКА ОСІЇ", "HOS"),
        ("КНИГА ПРОРОКА ЙОІЛА", "JOL"),
        ("КНИГА ПРОРОКА АМОСА", "AMO"),
        ("КНИГА ПРОРОКА АВДІЯ", "OBA"),
        ("КНИГА ПРОРОКА ЙОНИ", "JON"),
        ("КНИГА ПРОРОКА МІХЕЯ", "MIC"),
        ("КНИГА ПРОРОКА НАУМА", "NAM"),
        ("КНИГА ПРОРОКА АВВАКУМА", "HAB"),
        ("КНИГА ПРОРОКА СОФОНІЇ", "ZEP"),
        ("КНИГА ПРОРОКА АГГЕЮ", "HAG"),
        ("КНИГА ПРОРОКА ЗАХАРІЇ", "ZEC"),
        ("КНИГА ПРОРОКА МАЛАХІЇ", "MAL"),
        ("СВЯТА ЄВАНГЕЛІЯ ВІД МАТВІЯ", "MAT"),
        ("СВЯТА ЄВАНГЕЛІЯ ВІД МАРКА", "MRK"),
        ("СВЯТА ЄВАНГЕЛІЯ ВІД ЛУКИ", "LUK"),
        ("СВЯТА ЄВАНГЕЛІЯ ВІД ІВАНА", "JHN"),
        ("ДІЇ СВЯТИХ АПОСТОЛІВ", "ACT"),
        ("ПОСЛАННЯ АПОСТОЛА ПАВЛА ДО РИМЛЯН", "ROM"),
        ("ПЕРШЕ ПОСЛАННЯ АПОСТОЛА ПАВЛА ДО КОРИНТЯН", "1CO"),
        ("ДРУГЕ ПОСЛАННЯ АПОСТОЛА ПАВЛА ДО КОРИНТЯН", "2CO"),
        ("ПОСЛАННЯ АПОСТОЛА ПАВЛА ДО ГАЛАТІВ", "GAL"),
        ("ПОСЛАННЯ АПОСТОЛА ПАВЛА ДО ЕФЕСЯН", "EPH"),
        ("ПОСЛАННЯ АПОСТОЛА ПАВЛА ДО ФИЛИП'ЯН", "PHP"),
        ("ПОСЛАННЯ АПОСТОЛА ПАВЛА ДО КОЛОСЯН", "COL"),
        ("ПЕРШЕ ПОСЛАННЯ АПОСТОЛА ПАВЛА ДО СОЛУНЯН", "1TH"),
        ("ДРУГЕ ПОСЛАННЯ АПОСТОЛА ПАВЛА ДО СОЛУНЯН", "2TH"),
        ("ПЕРШЕ ПОСЛАННЯ АПОСТОЛА ПАВЛА ДО ТИМОФІЯ", "1TI"),
        ("ДРУГЕ ПОСЛАННЯ АПОСТОЛА ПАВЛА ДО ТИМОФІЯ", "2TI"),
        ("ПОСЛАННЯ АПОСТОЛА ПАВЛА ДО ТИТА", "TIT"),
        ("ПОСЛАННЯ АПОСТОЛА ПАВЛА ДО ФИЛИМОНА", "PHM"),
        ("ПОСЛАННЯ ДО ЄВРЕЇВ", "HEB"),
        ("ПОСЛАННЯ АПОСТОЛА ЯКОВА", "JAS"),
        ("ПЕРШЕ ПОСЛАННЯ АПОСТОЛА ПЕТРА", "1PE"),
        ("ДРУГЕ ПОСЛАННЯ АПОСТОЛА ПЕТРА", "2PE"),
        ("ПЕРШЕ ПОСЛАННЯ АПОСТОЛА ІВАНА", "1JN"),
        ("ДРУГЕ ПОСЛАННЯ АПОСТОЛА ІВАНА", "2JN"),
        ("ТРЕТЄ ПОСЛАННЯ АПОСТОЛА ІВАНА", "3JN"),
        ("ПОСЛАННЯ АПОСТОЛА ЮДИ", "JUD"),
        ("ОБ'ЯВЛЕННЯ ІВАНА БОГОСЛОВА", "REV")
    ];

    private static readonly IList<string> DaysOfWeek =
    [
        "Неділя. ",
        "Понеділок. ",
        "Вівторок. ",
        "Середа. ",
        "Четвер. ",
        "П'ятниця. "
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly IReadOnlyDictionary<string, string> BookNameCorrections =
        new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
        {
            ["Филм"] = "Филимона",
            ["Мих"] = "Міхея"
        };

    private static string _year = GetDefaultYear();
    private static string _quarter = GetDefaultQuarter();
    private static readonly List<int> SelectedWeeks = [];
    private static readonly HttpClient Client = new();

    private static string BaseUrl => $"https://sabbath-school-stage.adventech.io/api/v2/uk/quarterlies/{_year}-{_quarter}";

    public static async Task Run(Serilog.ILogger logger, string[]? args = null)
    {
        ParseArgs(args, logger);

        logger.Information("Creation of Sabbath School lessons!");

        // Fetch only the lessons we need
        var sss = await GetHeaders(logger);

        await CreateDocs(sss, logger);

        logger.Information("Sabbath School lessons were created!");
    }

    private static string GetDefaultYear() => DateTime.Now.Year.ToString();

    private static string GetDefaultQuarter()
    {
        var month = DateTime.Now.Month;
        return (((month - 1) / 3) + 1).ToString("D2");
    }

    private static void ParseArgs(string[]? args, Serilog.ILogger logger)
    {
        if (args is not null)
        {
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--year" when i + 1 < args.Length:
                        _year = args[++i];
                        break;
                    case "--quarter" when i + 1 < args.Length:
                        _quarter = args[++i];
                        break;
                    case "--week" when i + 1 < args.Length:
                        ParseWeekArg(args[++i]);
                        break;
                }
            }
        }

        var weekInfo = SelectedWeeks.Count > 0
            ? $", Weeks={string.Join(",", SelectedWeeks)}"
            : ", Weeks=all";
        logger.Information("Using Year={Year}, Quarter={Quarter}{WeekInfo}", _year, _quarter, weekInfo);
    }

    private static void ParseWeekArg(string weekArg)
    {
        if (string.IsNullOrWhiteSpace(weekArg))
            return;

        if (weekArg.Contains('-'))
        {
            var parts = weekArg.Split('-');
            if (parts.Length == 2 &&
                int.TryParse(parts[0].Trim(), out var start) &&
                int.TryParse(parts[1].Trim(), out var end) &&
                start > 0 && end > 0 && start <= end)
            {
                for (var w = start; w <= end; w++)
                {
                    if (!SelectedWeeks.Contains(w))
                    {
                        SelectedWeeks.Add(w);
                    }
                }
            }
        }
        else if (int.TryParse(weekArg.Trim(), out var week) && week > 0)
        {
            if (!SelectedWeeks.Contains(week))
            {
                SelectedWeeks.Add(week);
            }
        }
    }

    private static async Task<HttpResponseMessage> GetWithRetryAsync(string url)
    {
        var response = await RetryPipeline.ExecuteAsync(
            static async (state, ct) => await state.Client.GetAsync(state.Url, ct),
            (Client: Client, Url: url),
            CancellationToken.None);

        await Task.Delay(RequestThrottleDelay);

        return response;
    }

    private static async Task<IList<Ss>> GetHeaders(Serilog.ILogger logger)
    {
        var res = new List<Ss>();

        if (SelectedWeeks.Count > 0)
        {
            logger.Information("Getting lessons: {Weeks}", string.Join(", ", SelectedWeeks));
        }
        else
        {
            logger.Information("Getting all lessons");
        }

        var response = await GetWithRetryAsync($"{BaseUrl}/index.json");
        if (!response.IsSuccessStatusCode)
        {
            logger.Warning("Failed to fetch {Url}: {StatusCode}", $"{BaseUrl}/index.json", response.StatusCode);
            return res;
        }

        var content = await response.Content.ReadAsStringAsync();
        var titlesObj = JsonSerializer.Deserialize<QuarterlyIndexResponse>(content, JsonOptions);
        var lessonCounter = 0;
        var title = titlesObj?.Quarterly?.Title;
        foreach (var entry in titlesObj?.Lessons ?? Array.Empty<LessonIndexEntry>())
        {
            lessonCounter++;

            // Skip lessons that aren't selected (if weeks are specified)
            if (SelectedWeeks.Count > 0 && !SelectedWeeks.Contains(lessonCounter))
            {
                logger.Information("Skipping lesson {LessonNumber}", lessonCounter);
                continue;
            }

            var days = new List<Day>();
            var lessonInd = lessonCounter < 10 ? "0" + lessonCounter : lessonCounter.ToString();

            logger.Information("Getting lesson {LessonInd}", lessonInd);

            response = await GetWithRetryAsync($"{BaseUrl}/lessons/{lessonInd}/index.json");
            if (!response.IsSuccessStatusCode)
            {
                logger.Warning("Failed to fetch {Url}: {StatusCode}", $"{BaseUrl}/lessons/{lessonInd}/index.json", response.StatusCode);
                continue;
            }

            content = await response.Content.ReadAsStringAsync();
            var daysObj = JsonSerializer.Deserialize<LessonDaysResponse>(content, JsonOptions);
            var dayCounter = 0;
            var verse = string.Empty;
            foreach (var day in daysObj?.Days ?? Array.Empty<DayIndexEntry>())
            {
                if (day.Id == "teacher-comments" || day.Id == "commentary")
                {
                    continue;
                }

                logger.Debug("->Getting day {DayCounter}", dayCounter + 1);

                response = await GetWithRetryAsync($"{BaseUrl}/lessons/{lessonInd}/days/0{++dayCounter}/read/index.json");
                if (!response.IsSuccessStatusCode)
                {
                    logger.Warning("Failed to fetch {Url}: {StatusCode}", $"{BaseUrl}/lessons/{lessonInd}/days/0{dayCounter}/read/index.json", response.StatusCode);
                    continue;
                }

                content = await response.Content.ReadAsStringAsync();
                var dayObj = JsonSerializer.Deserialize<DayReadResponse>(content, JsonOptions);
                var lessonCont = dayObj?.Content ?? string.Empty;
                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(lessonCont);
                var element = htmlDocument.DocumentNode.SelectSingleNode("blockquote");
                var pTitle = element?.SelectSingleNode("p");
                if (pTitle is { InnerText: "Пам'ятний вірш" or "Пам'ятний текст" })
                {
                    verse = element?.InnerText.Trim().Replace("Пам'ятний вірш", string.Empty).Replace("Пам'ятний текст", string.Empty).Trim();
                }

                var questions = new List<Question>();
                var questionsObj= htmlDocument.DocumentNode.SelectNodes("//code");
                if (questionsObj != null)
                {
                    foreach (var questionNode in questionsObj)
                    {
                        var verses = new List<string>();
                        var qNodes = questionNode.SelectNodes(".//a[@class='verse']");
                        if (qNodes?.Any() ?? false)
                        {
                            var tmpVerses = qNodes.Select(x => x.InnerText).ToList();
                            verses.AddRange(tmpVerses.SelectMany(TextCleanup.ExtractVerseReferences));
                        }

                        var question = questionNode.InnerText.Trim();
                        question = StripQuestionLeadIn(question);
                        questions.Add(new Question(verses, question));
                    }
                }

                logger.Debug("-> Adding new day {DayCounter}", dayCounter);

                days.Add(new Day
                {
                    Title = day.Title ?? "",
                    EndDate = DateTime.ParseExact(day.Date ?? "", "dd/MM/yyyy", CultureInfo.InvariantCulture),
                    Url = day.FullPath ?? "",
                    Questions = questions
                });
            }

            logger.Information("Adding lesson {LessonInd}", lessonInd);

            res.Add(new Ss
            {
                LessonTitle = title ?? "",
                Title = entry.Title ?? "",
                EndDate = DateTime.ParseExact(entry.EndDate ?? "", "dd/MM/yyyy", CultureInfo.InvariantCulture),
                Url = entry.FullPath ?? "",
                MemoryVerse = TextCleanup.ParseMemoryVerse(verse),
                Days = days
            });
        }

        return res;
    }

    private static string StripQuestionLeadIn(string text)
    {
        text = Regex.Replace(text, @"^\d+\. ", "");
        text = Regex.Replace(text, @"^Прочитайте\s+тексти\s*", "");
        text = Regex.Replace(text, @"^Перегляньте\s+уривок\s*", "");
        text = Regex.Replace(text, @"^Прочитайте\s+уривок\s*", "");
        text = Regex.Replace(text, @"^Прочитайте\s*", "");
        text = Regex.Replace(text, @"^\s*див.\s*", "");
        text = Regex.Replace(text, @"^\s*текст\s*", "");
        return text;
    }

    private static string StripVerseRemnant(string text)
    {
        text = Regex.Replace(text, @"\s*\((?:Прочитайте|див\.|Див\.\s+също)?\s*\)\.*", "");
        text = Regex.Replace(text, @"^\s*[\.\:\;] ", "");
        text = Regex.Replace(text, @"^\s*та ", "");
        text = Regex.Replace(text, @"^\s*і ", "");
        return text;
    }

    private static XWPFDocument InitializeDocument(Ss ss, int lessonNumber)
    {
        // Load template to preserve all styles (Title, Subtitle, Heading2, Heading3, Normal, IntenseQuote)
        using var templateFileStream = File.OpenRead("Template.docx");
        var doc = new XWPFDocument(templateFileStream);

        // Clear body while keeping all template styles
        while (doc.RemoveBodyElement(0)) { }

        return doc
            .AddParagraph("Title", ss.LessonTitle)
            .AddParagraph("Subtitle", $"{lessonNumber}. {ss.Title}")
            .AddParagraph("Heading2", "Вступ")
            .AddParagraph("Normal", "Привітання");
    }

    private static void AddHyperlinkToRun(XWPFParagraph para, string text, string uri, string color)
    {
        // Add relationship directly and use its ID
        // This creates direct-URL relationships instead of the double-layer that CreateHyperlinkRun produces
        var rel = para.Document.GetPackagePart().AddExternalRelationship(
            uri,
            XWPFRelation.HYPERLINK.Relation
        );
        string rId = rel.Id;

        // Manually create hyperlink element via XML to avoid double-wrapping
        var paraCtp = para.GetCTP();
        var hyperlink = paraCtp.AddNewHyperlink();
        hyperlink.id = rId;
        hyperlink.history = NPOI.OpenXmlFormats.Wordprocessing.ST_OnOff.on;  // Enable hyperlink history tracking

        var run = hyperlink.AddNewR();
        var rPr = run.AddNewRPr();

        // Apply Hyperlink character style (CRITICAL for Word recognition)
        var rStyle = rPr.AddNewRStyle();
        rStyle.val = "Hyperlink";

        // Set color and underline
        var colorElem = rPr.AddNewColor();
        colorElem.val = color;
        var underline = rPr.AddNewU();
        underline.val = NPOI.OpenXmlFormats.Wordprocessing.ST_Underline.single;

        // Set text
        var t = run.AddNewT();
        t.Value = text;
    }

    private static XWPFDocument AddMemoryVerseSection(XWPFDocument doc, (string, string) memoryVerse)
    {
        doc.AddParagraph("Heading3", "Пам'ятний вірш");

        var para = doc.CreateParagraph().WithStyle("IntenseQuote");
        var uri = GetBibleLink(memoryVerse);
        AddHyperlinkToRun(para, memoryVerse.Item1, uri, "0563C1");

        para.CreateRun()
            .WithBreak()
            .WithAppendedText(memoryVerse.Item2);

        return doc;
    }

    private static XWPFDocument AddQuestionSection(XWPFDocument doc) =>
        doc
            .AddParagraph("Heading3", "Питання уроку:")
            .AddParagraph("Normal", "Ділимося на 3 класи. До 11:10.")
            .AddParagraph("Heading2", "Початок уроку")
            .AddParagraph("Normal", "→ ")
            .AddParagraph("Heading2", "Пам'ятний вірш")
            .AddParagraph("Normal", "→ ");

    private static XWPFDocument AddDaysSection(XWPFDocument doc, IList<Day> days)
    {
        var dayCount = Math.Min(DaysOfWeek.Count, days.Count - 1);
        for (var j = 0; j < dayCount; j++)
        {
            var day = days[j + 1];
            doc.AddParagraph("Heading2", DaysOfWeek[j] + day.Title);
            AddQuestionsForDay(doc, day.Questions);
        }

        return doc;
    }

    private static void AddQuestionsForDay(XWPFDocument doc, IList<Question> questions)
    {
        foreach (var question in questions)
        {
            if (question.Verses.Any())
            {
                AddQuestionWithVerses(doc, question);
            }
            else
            {
                AddPlainQuestion(doc, question);
            }
        }
    }

    private static void AddQuestionWithVerses(XWPFDocument doc, Question question)
    {
        var para = doc.CreateParagraph().WithStyle("Normal");
        var run = para.CreateRun();

        var innerText = question.Text.Replace("–", "-");

        foreach (var verse in question.Verses)
        {
            run.SetText("→ ");
            var uri = GetBibleLink((verse, verse));
            var verseText = question.Verses.Count > 1 ? verse + "; " : verse;
            AddHyperlinkToRun(para, verseText, uri, "0563C1");

            innerText = innerText.Replace(verse, "");
            innerText = StripVerseRemnant(innerText);
        }

        run = para.CreateRun();
        run.SetText($" {innerText}");
    }

    private static void AddPlainQuestion(XWPFDocument doc, Question question)
    {
        var para = doc
            .CreateParagraph()
            .WithStyle("Normal");
        para
            .CreateRun()
            .SetText($"→ {question.Text}");
    }

    private static XWPFDocument FinalizeDocument(XWPFDocument doc)
    {
        return doc
            .AddParagraph("Heading2", "Закінчення")
            .AddParagraph("Normal", " ")
            .AddParagraph("Heading3", "Молитва");
    }

    private static async Task CreateDocs(IList<Ss> sss, Serilog.ILogger logger)
    {
        if (sss.Count == 0)
        {
            logger.Warning("No documents to generate");
            return;
        }

        logger.Information("Creating documents");
        logger.Information("Documents to generate: {Count}", sss.Count);

        // Re-map to actual week numbers if filtering was applied
        var startWeek = SelectedWeeks.Count > 0 ? SelectedWeeks[0] : 1;

        for (var i = 0; i < sss.Count; i++)
        {
            var ss = sss[i];
            var weekNumber = startWeek + i;

            logger.Debug("Creating document for week {WeekNumber}: {Title}", weekNumber, ss.Title);

            var doc = InitializeDocument(ss, weekNumber)
                .Pipe(AddMemoryVerseSection, ss.MemoryVerse)
                .Pipe(AddQuestionSection)
                .Pipe(AddDaysSection, ss.Days)
                .Pipe(FinalizeDocument);

            logger.Debug("Saving document {Title}", ss.Title);

            var fileName = $"Суботня школа {ss.EndDate:yyyy}.{_quarter}." + (weekNumber < 10 ? "0" : string.Empty) + weekNumber + ".docx";
            await using var sw = File.Create(fileName);
            doc.Write(sw);
            doc.Close();
        }

        logger.Information("All documents were saved");
    }

    private static string GetBibleLink((string, string) where)
    {
        var url = "https://www.google.com/search?q=" + where.Item2;
        if (string.IsNullOrEmpty(where.Item1) && string.IsNullOrEmpty(where.Item2))
        {
            return url;
        }

        var refer = string.Empty;
        var verse = where.Item1.Split(' ');
        var bookName = verse[0].Trim('.').Length == 1 ? verse[1].Trim('.') : verse[0].Trim('.');
        bookName = NormalizeBookName(bookName);
        foreach (var book in BooksOfBible)
        {
            if (book.Key.Contains(bookName, StringComparison.InvariantCultureIgnoreCase))
            {
                refer = book.Value;
                break;
            }
        }

        if (string.IsNullOrEmpty(refer))
        {
            return url;
        }

        var bookId = verse[0].Trim('.').Length == 1 ? verse[0].Trim('.') : string.Empty;
        if (!string.IsNullOrEmpty(bookId))
        {
            refer = bookId[0] + refer.Remove(0, 1);
            var fnd = string.Join("", verse.Skip(2)).Replace(" ", null).Replace(':', '.');
            url = $"{BibleUrl}{refer}.{fnd}";
        }
        else
        {
            url = $"{BibleUrl}{refer}.{where.Item1.Replace(verse[0], null).Replace(" ", null).Replace(':', '.')}";
        }

        return url;
    }

    private static string NormalizeBookName(string bookName)
    {
        foreach (var correction in BookNameCorrections.OrderByDescending(c => c.Key.Length))
        {
            if (bookName.StartsWith(correction.Key, StringComparison.InvariantCultureIgnoreCase))
            {
                return correction.Value;
            }
        }

        return bookName;
    }
}
