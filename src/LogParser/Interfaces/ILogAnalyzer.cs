using LogParser.Models;

namespace LogParser.Interfaces;

/// <summary>
/// Contract for analyzing parsed log entries. Decoupled from parsing
/// and reporting — accepts any IAsyncEnumerable source.
/// </summary>
public interface ILogAnalyzer
{
    /// <summary>
    /// Performs a single-pass analysis over the stream of log entries.
    /// </summary>
    Task<AnalysisResult> AnalyzeAsync(
        IAsyncEnumerable<LogEntry> entries,
        int topN = 3,
        CancellationToken cancellationToken = default);
}
