using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Newtonsoft.Json;
using NPOI.XWPF.UserModel;

namespace SabbathSchoolLessonBuilder
{
    public class WebScrapping
    {
        private const string Year = "2026";
        private const string Quarter = "02";
        private const string BaseUrl = $"https://sabbath-school-stage.adventech.io/api/v2/uk/quarterlies/{Year}-{Quarter}";
        private const string BibleUrl = "https://www.bible.com/uk/bible/3786/";

        private static readonly IReadOnlyList<(string Key, string Value)> BooksOfBible = new List<(string Key, string Value)>
        {
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
            ("КНИГА ПРОРОКА АГГЕЯ", "HAG"),
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
        };
        private static readonly IList<string> DaysOfWeek = new List<string>
        {
            "Неділя. ",
            "Понеділок. ",
            "Вівторок. ",
            "Середа. ",
            "Четвер. ",
            "П'ятниця. "
        };

        // Abbreviated book names that don't line up with a substring match against
        // BooksOfBible (e.g. Russian-spelled abbreviations vs. Ukrainian full names,
        // or abbreviations that skip letters present in the full name) get corrected
        // here before the lookup in GetBibleLink.
        private static readonly IReadOnlyDictionary<string, string> BookNameCorrections =
            new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
        {
            ["Филм"] = "Филимона",
            ["Мих"] = "Міхея"
        };

        public static async Task Run(Serilog.ILogger logger)
        {
            logger.Information("Creation of Sabbath School lessons!");

            var sss = await GetHeaders(logger);
            await CreateDocs(sss, logger);

            logger.Information("Sabbath School lessons were created!");
        }

        private static async Task<IList<Ss>> GetHeaders(Serilog.ILogger logger)
        {
            var res = new List<Ss>();
            var client = new HttpClient();

            logger.Information("Getting all lessons");

            var response = await client.GetAsync($"{BaseUrl}/index.json");
            if (!response.IsSuccessStatusCode)
            {
                logger.Warning("Failed to fetch {Url}: {StatusCode}", $"{BaseUrl}/index.json", response.StatusCode);
                return res;
            }

            var content = await response.Content.ReadAsStringAsync();
            dynamic titlesObj = JsonConvert.DeserializeObject(content) ?? string.Empty;
            var lessonCounter = 0;
            var title = titlesObj["quarterly"]["title"].Value;
            foreach (dynamic entry in titlesObj["lessons"])
            {
                var days = new List<Day>();
                var lessonInd = lessonCounter++ < 9 ? "0" + lessonCounter : lessonCounter.ToString();

                logger.Information("Getting lesson {LessonInd}", lessonInd);

                response = await client.GetAsync($"{BaseUrl}/lessons/{lessonInd}/index.json");
                if (!response.IsSuccessStatusCode)
                {
                    logger.Warning("Failed to fetch {Url}: {StatusCode}", $"{BaseUrl}/lessons/{lessonInd}/index.json", response.StatusCode);
                    continue;
                }

                content = await response.Content.ReadAsStringAsync();
                dynamic daysObj = JsonConvert.DeserializeObject(content);
                var dayCounter = 0;
                var verse = string.Empty;
                foreach (var day in daysObj["days"])
                {
                    if (day["id"] == "teacher-comments" || day["id"] == "commentary")
                    {
                        continue;
                    }

                    logger.Debug("->Getting day {DayCounter}", dayCounter + 1);

                    response = await client.GetAsync($"{BaseUrl}/lessons/{lessonInd}/days/0{++dayCounter}/read/index.json");
                    if (!response.IsSuccessStatusCode)
                    {
                        logger.Warning("Failed to fetch {Url}: {StatusCode}", $"{BaseUrl}/lessons/{lessonInd}/days/0{dayCounter}/read/index.json", response.StatusCode);
                        continue;
                    }

                    content = await response.Content.ReadAsStringAsync();
                    dynamic dayObj = JsonConvert.DeserializeObject(content);
                    var lessonCont = dayObj["content"].ToString();
                    var htmlDocument = new HtmlDocument();
                    htmlDocument.LoadHtml(lessonCont);
                    var element = htmlDocument.DocumentNode.SelectSingleNode("blockquote");
                    if (element != null)
                    {
                        var pTitle = element.SelectSingleNode("p");
                        if (pTitle != null && (pTitle.InnerText == "Пам’ятний вірш" || pTitle.InnerText == "Пам’ятний текст"))
                        {
                            verse = element.InnerText.Trim().Replace("Пам’ятний вірш", string.Empty).Replace("Пам’ятний текст", string.Empty).Trim();
                        }
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
                                var tmpVerses = qNodes.Select(x => x.InnerText.Replace("–", "-")).ToList();
                                foreach (var tmpVerse in tmpVerses)
                                {
                                    var pattern = @"([\dІ\.]{0,2}\s*[а-яА-ЯІїєюіЇЄЮ’]+\.{0,1}\s+\d+\:[\d,\-\s]+)(?:(?![\dІ\.]{0,2}\s*[а-яА-ЯІїєюіЇЄЮ’]+\.{0,1}\s+\d+\:[\d,\-\s]+).)*";
                                    var matches = Regex.Matches(tmpVerse, pattern);
                                    foreach (Match m in matches)
                                    {
                                        var tmpValue = Regex.Replace(m.Value.Trim(), @"^\s*[\.\:\;] ", "");
                                        tmpValue = Regex.Replace(tmpValue, @"\s*[\,\;\.]$", "");
                                        verses.Add(tmpValue);
                                    }
                                }
                            }

                            var question = questionNode.InnerText.Trim();
                            question = Regex.Replace(question, @"^\d+\. ", "");
                            question = Regex.Replace(question, @"^Прочитайте\s+тексти\s*", "");
                            question = Regex.Replace(question, @"^Перегляньте\s+уривок\s*", "");
                            question = Regex.Replace(question, @"^Прочитайте\s+уривок\s*", "");
                            question = Regex.Replace(question, @"^Прочитайте\s*", "");
                            question = Regex.Replace(question, @"^\s*див.\s*", "");
                            question = Regex.Replace(question, @"^\s*текст\s*", "");
                            questions.Add(new Question(verses, question));
                        }
                    }

                    logger.Debug("-> Adding new day {DayCounter}", dayCounter);

                    days.Add(new Day
                    {
                        Title = day["title"],
                        EndDate = DateTime.ParseExact(day["date"].ToString(), "dd/MM/yyyy", CultureInfo.InvariantCulture),
                        Url = day["full_path"],
                        Questions = questions
                    });
                }

                logger.Information("Adding lesson {LessonInd}", lessonInd);

                res.Add(new Ss
                {
                    LessonTitle = title,
                    Title = entry["title"],
                    EndDate = DateTime.ParseExact(entry["end_date"].ToString(), "dd/MM/yyyy", CultureInfo.InvariantCulture),
                    Url = entry["full_path"],
                    MemoryVerse = GetVerse(verse),
                    Days = days
                });
            }

            return res;
        }

        private static (string, string) GetVerse(string verse)
        {
            var res = string.Empty;
            if (string.IsNullOrWhiteSpace(verse) || verse.IndexOf('(') < 0)
            {
                return (res, res);
            }

            var openIdx = verse.IndexOf('(');
            var closeIdx = verse.IndexOf(')');
            if (closeIdx < 0 || closeIdx < openIdx)
            {
                return (res, res);
            }

            var bbl = verse.Substring(openIdx + 1, closeIdx - openIdx - 1);

            var trimmed = verse.TrimStart('«');
            var closingGuillemetIdx = trimmed.IndexOf('»');
            if (closingGuillemetIdx < 1)
            {
                return (res, res);
            }

            res = trimmed.Substring(0, closingGuillemetIdx - 1);
            return (bbl, res);
        }

        private static async Task CreateDocs(IList<Ss> sss, Serilog.ILogger logger)
        {
            logger.Information("Creating documents");

            for (int i = 0; i < sss.Count; i++)
            {
                var ss = sss[i];

                logger.Debug("Creating document {Title}", ss.Title);

                await using var templateFileStream = File.OpenRead("Template.docx");
                XWPFDocument doc = new XWPFDocument(templateFileStream);

                // Delete contents
                while (doc.RemoveBodyElement(0)) { }

                AddParagraph(doc, "Title", $"{ss.LessonTitle}");

                AddParagraph(doc, "Subtitle", $"{i + 1}. {ss.Title}");

                AddParagraph(doc, "Heading2", "Вступ");

                AddParagraph(doc, "Normal", "Привітання");

                AddParagraph(doc, "Heading3", "Пам’ятний вірш");

                XWPFParagraph para1 = doc.CreateParagraph();
                para1.Style = "IntenseQuote";

                var hyperlinkRun = CreateHyperlinkRun(para1, GetBibleLink(ss.MemoryVerse));
                hyperlinkRun.SetText(ss.MemoryVerse.Item1);
                hyperlinkRun.SetColor("0563C1");
                hyperlinkRun.Underline = UnderlinePatterns.Single;

                XWPFRun run1 = para1.CreateRun();
                run1.AddBreak(BreakType.TEXTWRAPPING);
                run1.AppendText(ss.MemoryVerse.Item2);

                AddParagraph(doc, "Heading3", "Питання уроку:");

                AddParagraph(doc, "Normal", "Ділимося на 3 класи. До 11:10.");

                AddParagraph(doc, "Heading2", "Початок уроку");

                AddParagraph(doc, "Normal", "\u2192 ");

                AddParagraph(doc, "Heading2", "Пам’ятний вірш");

                AddParagraph(doc, "Normal", "\u2192 ");

                for (var j = 0; j < Math.Min(DaysOfWeek.Count, ss.Days.Count - 1); j++)
                {
                    var day = ss.Days[j + 1];
                    AddParagraph(doc, "Heading2", DaysOfWeek[j] + day.Title);

                    foreach (var question in day.Questions)
                    {
                        para1 = doc.CreateParagraph();
                        para1.Style = "Normal";
                        run1 = para1.CreateRun();

                        if (question.Verses.Any())
                        {
                            var innerText = question.Text;
                            innerText = innerText.Replace("–", "-");

                            foreach (var verse in question.Verses)
                            {
                                run1.SetText("\u2192 ");

                                hyperlinkRun = CreateHyperlinkRun(para1, GetBibleLink((verse, verse)));
                                var tmpTxt = question.Verses.Count > 1 ? verse + "; " : verse;
                                hyperlinkRun.SetText(tmpTxt);
                                hyperlinkRun.SetColor("0563C1");
                                hyperlinkRun.Underline = UnderlinePatterns.Single;
                                innerText = innerText.Replace(verse, "");
                                innerText = Regex.Replace(innerText, @"\s*\((?:Прочитайте|див\.|Див\.\s+також)?\s*\)\.*", "");
                                innerText = Regex.Replace(innerText, @"^\s*[\.\:\;] ", "");
                                innerText = Regex.Replace(innerText, @"^\s*та ", "");
                                innerText = Regex.Replace(innerText, @"^\s*і ", "");
                            }
                            run1 = para1.CreateRun();
                            run1.SetText($" {innerText}");
                        }
                        else
                        {
                            run1.SetText($"\u2192 {question.Text}");
                        }
                    }
                }

                AddParagraph(doc, "Heading2", "Закінчення");

                AddParagraph(doc, "Normal", " ");

                AddParagraph(doc, "Heading3", "Молитва");

                logger.Debug("Saving document {Title}", ss.Title);

                var fileName = $"Суботня школа {ss.EndDate:yyyy}.{Quarter}." + (i < 9 ? "0" : string.Empty) + (i + 1) + ".docx";
                await using var sw = File.Create(fileName);
                doc.Write(sw);
                doc.Close();
            }

            logger.Information("All documents were saved");
        }

        private static XWPFRun AddParagraph(XWPFDocument doc, string style, string text)
        {
            var paragraph = doc.CreateParagraph();
            paragraph.Style = style;
            var run = paragraph.CreateRun();
            run.SetText(text);
            return run;
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

            // Об’явл. 12:7, 8
            // 
            if (!string.IsNullOrEmpty(refer))
            {
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
            }

            return url;
        }

        /// <summary>
        /// Corrects abbreviated Bible book names that don't substring-match their
        /// full name in <see cref="BooksOfBible"/> (e.g. abbreviations spelled with
        /// Russian letters instead of Ukrainian ones). Pure lookup, no side effects,
        /// so it can be tested independently of <see cref="GetBibleLink"/>.
        /// Matches are checked longest-key-first so results stay deterministic even
        /// if a future correction's key happens to be a prefix of another one's.
        /// </summary>
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

        private static XWPFHyperlinkRun CreateHyperlinkRun(XWPFParagraph paragraph, string uri)
        {
            string rId = paragraph.Document.GetPackagePart().AddExternalRelationship(
                uri,
                XWPFRelation.HYPERLINK.Relation
            ).Id;

            return paragraph.CreateHyperlinkRun(rId);
        }

    }
}
