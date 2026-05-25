using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using LogParser.Interfaces;
using LogParser.Models;

namespace LogParser.Services;

/// <summary>
/// Parses Apache/Nginx Combined Log Format lines using a compiled regex.
/// Handles edge cases: absolute URLs, trailing data after user-agent,
/// authenticated vs anonymous users, and all HTTP status codes.
/// </summary>
public sealed class AccessLogParser : ILogParser
{
    private readonly ILogger<AccessLogParser> _logger;

    private static readonly Regex LogPattern = new(
        @"^(?<ip>\S+)\s+(?<ident>\S+)\s+(?<user>\S+)\s+\[(?<timestamp>[^\]]+)\]\s+""(?<method>\S+)\s+(?<url>\S+)\s+(?<protocol>\S+)""\s+(?<status>\d{3})\s+(?<size>\d+)\s+""(?<referer>[^""]*)""\s+""(?<ua>[^""]*)""",
        RegexOptions.Compiled);

    // Strips scheme + host from absolute URLs: "http://example.net/faq/" → "/faq/"
    private static readonly Regex AbsoluteUrlPattern = new(
        @"^https?://[^/]+(/.*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public AccessLogParser(ILogger<AccessLogParser> logger)
    {
        _logger = logger;
    }

    public async IAsyncEnumerable<LogEntry> ParseFileAsync(
        string filePath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Log file not found: {filePath}", filePath);

        var lineNumber = 0;

        // StreamReader for memory-efficient line-by-line reading.
        // Memory stays constant regardless of file size.
        using var reader = new StreamReader(filePath);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var entry = ParseLine(line);

            if (entry is null)
            {
                _logger.LogWarning(
                    "Failed to parse line {LineNumber}: {RawLine}",
                    lineNumber, TruncateForLog(line));
                continue;
            }

            yield return entry;
        }

        _logger.LogInformation("Parsed {LineCount} lines from {FilePath}", lineNumber, filePath);
    }

    public LogEntry? ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var match = LogPattern.Match(line);
        if (!match.Success)
            return null;

        var normalizedUrl = NormalizeUrl(match.Groups["url"].Value);

        if (!TryParseTimestamp(match.Groups["timestamp"].Value, out var timestamp))
            return null;

        return new LogEntry(
            IpAddress: match.Groups["ip"].Value,
            Identity: match.Groups["ident"].Value,
            User: match.Groups["user"].Value,
            Timestamp: timestamp,
            Method: match.Groups["method"].Value,
            Url: normalizedUrl,
            Protocol: match.Groups["protocol"].Value,
            StatusCode: int.Parse(match.Groups["status"].Value),
            ResponseSize: int.Parse(match.Groups["size"].Value),
            UserAgent: match.Groups["ua"].Value);
    }

    /// <summary>
    /// Normalizes URLs to path-only form. Absolute URLs like
    /// "http://example.net/faq/" become "/faq/" so proxy-style and
    /// relative requests count as the same resource.
    /// </summary>
    public static string NormalizeUrl(string rawUrl)
    {
        var absoluteMatch = AbsoluteUrlPattern.Match(rawUrl);
        return absoluteMatch.Success ? absoluteMatch.Groups[1].Value : rawUrl;
    }

    private static bool TryParseTimestamp(string raw, out DateTimeOffset result)
    {
        // Format: 10/Jul/2018:22:21:28 +0200
        return DateTimeOffset.TryParseExact(
            raw,
            "dd/MMM/yyyy:HH:mm:ss zzz",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out result);
    }

    private static string TruncateForLog(string value, int maxLength = 200)
        => value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), "...");
}
