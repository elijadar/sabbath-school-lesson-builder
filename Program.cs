using Serilog;

namespace SabbathSchoolLessonBuilder;

public class Program
{
    static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        await WebScrapping.Run(Log.Logger, args);
    }
}