namespace LogParser.Models;

/// <summary>
/// Represents a single parsed log entry. Uses a record for value-based equality
/// and immutability — once parsed, a log entry should never be modified.
/// </summary>
public sealed record LogEntry(
    string IpAddress,
    string Identity,
    string User,
    DateTimeOffset Timestamp,
    string Method,
    string Url,
    string Protocol,
    int StatusCode,
    int ResponseSize,
    string UserAgent);
