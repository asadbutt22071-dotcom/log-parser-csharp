using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LogParser.Interfaces;
using LogParser.Services;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: LogParser <logfile> [--top-n N] [--verbose]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Arguments:");
            Console.Error.WriteLine("  logfile       Path to the log file to analyze");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  --top-n N         Number of top URLs/IPs to display (default: 3)");
            Console.Error.WriteLine("  --format text|json  Output format (default: text)");
            Console.Error.WriteLine("  --verbose         Enable debug-level logging");
            return 1;
        }

        var filePath = args[0];
        var topN = 3;
        var verbose = false;
        var format = "text";

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--top-n" or "-n" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out topN) || topN < 1)
                    {
                        Console.Error.WriteLine("Error: --top-n must be a positive integer");
                        return 1;
                    }
                    break;
                case "--format" or "-f" when i + 1 < args.Length:
                    format = args[++i].ToLowerInvariant();
                    if (format is not ("text" or "json"))
                    {
                        Console.Error.WriteLine("Error: --format must be 'text' or 'json'");
                        return 1;
                    }
                    break;
                case "--verbose" or "-v":
                    verbose = true;
                    break;
            }
        }

        var services = new ServiceCollection();

        services.AddLogging(logging =>
        {
            logging.AddSimpleConsole(options => options.SingleLine = true);
            logging.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Warning);
        });

        services.AddSingleton<ILogParser, AccessLogParser>();
        services.AddSingleton<ILogAnalyzer, LogAnalyzer>();
        services.AddSingleton<IReporter>(_ => format switch
        {
            "json" => new JsonReporter(),
            _      => new ConsoleReporter()
        });

        using var provider = services.BuildServiceProvider();

        var parser = provider.GetRequiredService<ILogParser>();
        var analyzer = provider.GetRequiredService<ILogAnalyzer>();
        var reporter = provider.GetRequiredService<IReporter>();

        try
        {
            var entries = parser.ParseFileAsync(filePath);
            var result = await analyzer.AnalyzeAsync(entries, topN);
            await reporter.ReportAsync(result);
            return 0;
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            if (verbose)
                Console.Error.WriteLine(ex.StackTrace);
            return 2;
        }
    }
}