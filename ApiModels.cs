using System.Text.Json.Serialization;

namespace SabbathSchoolLessonBuilder
{
    // Typed shapes for the sabbath-school-stage.adventech.io API responses consumed by GetHeaders.

    public class QuarterlyIndexResponse
    {
        public QuarterlyInfo? Quarterly { get; set; }
        public IList<LessonIndexEntry>? Lessons { get; set; }
    }

    public class QuarterlyInfo
    {
        public string? Title { get; set; }
    }

    public class LessonIndexEntry
    {
        public string? Title { get; set; }

        [JsonPropertyName("end_date")]
        public string? EndDate { get; set; }

        [JsonPropertyName("full_path")]
        public string? FullPath { get; set; }
    }

    public class LessonDaysResponse
    {
        public IList<DayIndexEntry>? Days { get; set; }
    }

    public class DayIndexEntry
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Date { get; set; }

        [JsonPropertyName("full_path")]
        public string? FullPath { get; set; }
    }

    public class DayReadResponse
    {
        public string? Content { get; set; }
    }
}
