using LogParser.Interfaces;
using LogParser.Models;

namespace LogParser.Services;

/// <summary>
/// Renders analysis results to the console. Implements IReporter so it can
/// be swapped for HTML, JSON, or cloud-based reporters via DI.
/// </summary>
public sealed class ConsoleReporter : IReporter
{
    private readonly TextWriter _output;

    /// <summary>
    /// Accepts a TextWriter for testability — in production this is Console.Out,
    /// in tests it's a StringWriter for assertion.
    /// </summary>
    public ConsoleReporter(TextWriter? output = null)
    {
        _output = output ?? Console.Out;
    }

    public async Task ReportAsync(AnalysisResult result, CancellationToken cancellationToken = default)
    {
        await _output.WriteLineAsync("═══════════════════════════════════════════════════");
        await _output.WriteLineAsync("  Log Analysis Report");
        await _output.WriteLineAsync("═══════════════════════════════════════════════════");
        await _output.WriteLineAsync();

        await _output.WriteLineAsync($"  Total requests processed: {result.TotalRequests}");
        await _output.WriteLineAsync($"  Unique IP addresses:      {result.UniqueIpCount}");
        await _output.WriteLineAsync();

        await _output.WriteLineAsync("───────────────────────────────────────────────────");
        await _output.WriteLineAsync($"  Top {result.TopUrls.Count} Most Visited URLs");
        await _output.WriteLineAsync("───────────────────────────────────────────────────");

        for (var i = 0; i < result.TopUrls.Count; i++)
        {
            var (url, count) = result.TopUrls[i];
            await _output.WriteLineAsync($"  {i + 1}. {url} ({count} requests)");
        }

        await _output.WriteLineAsync();
        await _output.WriteLineAsync("───────────────────────────────────────────────────");
        await _output.WriteLineAsync($"  Top {result.TopIpAddresses.Count} Most Active IP Addresses");
        await _output.WriteLineAsync("───────────────────────────────────────────────────");

        for (var i = 0; i < result.TopIpAddresses.Count; i++)
        {
            var (ip, count) = result.TopIpAddresses[i];
            await _output.WriteLineAsync($"  {i + 1}. {ip} ({count} requests)");
        }

        await _output.WriteLineAsync();
        await _output.WriteLineAsync("───────────────────────────────────────────────────");
        await _output.WriteLineAsync("  Status Code Distribution");
        await _output.WriteLineAsync("───────────────────────────────────────────────────");

        foreach (var (status, count) in result.StatusCodeDistribution.OrderBy(kv => kv.Key))
        {
            await _output.WriteLineAsync($"  {status}: {count} requests");
        }

        await _output.WriteLineAsync();
        await _output.WriteLineAsync("═══════════════════════════════════════════════════");
    }
}
