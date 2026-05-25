namespace LogParser.Models;

/// <summary>
/// Encapsulates the complete analysis output. Immutable once computed.
/// </summary>
public sealed record AnalysisResult(
    int UniqueIpCount,
    IReadOnlyList<(string Url, int Count)> TopUrls,
    IReadOnlyList<(string IpAddress, int Count)> TopIpAddresses,
    int TotalRequests,
    int FailedParseCount,
    IReadOnlyDictionary<int, int> StatusCodeDistribution);
