namespace SabbathSchoolLessonBuilder;

public class Ss
{
    public string LessonTitle { get; set; } = "";
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTime EndDate { get; set; }
    public (string, string) MemoryVerse { get; set; }

    public IList<Day> Days { get; set; } = [];
}

public class Question
{
    public Question()
    {
        Verses = [];
        Text = "";
    }

    public Question(IList<string> verses, string text)
    {
        Verses = verses;
        Text = text;
    }

    public IList<string> Verses { get; set; }
    public string Text { get; set; }

}

public class Day
{
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTime EndDate { get; set; }
    public IList<Question> Questions { get; set; } = [];
}