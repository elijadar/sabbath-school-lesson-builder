using Serilog;

namespace WebScrapping._SS
{
    public class Program
    {
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

            WebScrapping.Run(Log.Logger);
        }
    }
}
