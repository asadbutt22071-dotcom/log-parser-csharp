using Microsoft.Extensions.Logging;
using LogParser.Interfaces;
using LogParser.Models;

namespace LogParser.Services;

/// <summary>
/// Single-pass analysis over a stream of log entries.
/// Uses Dictionary for frequency counting and HashSet for uniqueness —
/// O(1) amortized insert/lookup, same pattern as Python's Counter + set.
/// </summary>
public sealed class LogAnalyzer : ILogAnalyzer
{
    private readonly ILogger<LogAnalyzer> _logger;

    public LogAnalyzer(ILogger<LogAnalyzer> logger)
    {
        _logger = logger;
    }

    public async Task<AnalysisResult> AnalyzeAsync(
        IAsyncEnumerable<LogEntry> entries,
        int topN = 3,
        CancellationToken cancellationToken = default)
    {
        if (topN < 1)
            throw new ArgumentOutOfRangeException(nameof(topN), "topN must be at least 1");

        var urlCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ipCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var statusCounts = new Dictionary<int, int>();
        var totalRequests = 0;

        await foreach (var entry in entries.WithCancellation(cancellationToken))
        {
            totalRequests++;

            urlCounts[entry.Url] = urlCounts.GetValueOrDefault(entry.Url) + 1;
            ipCounts[entry.IpAddress] = ipCounts.GetValueOrDefault(entry.IpAddress) + 1;
            statusCounts[entry.StatusCode] = statusCounts.GetValueOrDefault(entry.StatusCode) + 1;
        }

        var topUrls = urlCounts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Take(topN)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();

        var topIps = ipCounts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Take(topN)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();

        _logger.LogInformation(
            "Analysis complete: {Total} requests, {UniqueIps} unique IPs",
            totalRequests, ipCounts.Count);

        return new AnalysisResult(
            UniqueIpCount: ipCounts.Count,
            TopUrls: topUrls,
            TopIpAddresses: topIps,
            TotalRequests: totalRequests,
            FailedParseCount: 0,
            StatusCodeDistribution: statusCounts);
    }
}
