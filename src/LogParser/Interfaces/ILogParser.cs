using LogParser.Models;

namespace LogParser.Interfaces;

/// <summary>
/// Contract for log line parsing. Implementations handle format-specific
/// regex/parsing logic. The async enumerable return type enables streaming
/// — today from a file, tomorrow from S3, Kafka, or a chunked parallel reader.
/// </summary>
public interface ILogParser
{
    /// <summary>
    /// Parses a log file and yields structured entries one at a time.
    /// Malformed lines are logged and skipped, not fatal.
    /// </summary>
    IAsyncEnumerable<LogEntry> ParseFileAsync(string filePath, CancellationToken cancellationToken = default);
}
