namespace LogParser.Models;

/// <summary>
/// Thrown when a log line cannot be parsed. Carries structured context
/// (line number, raw content) for diagnostics. In a cloud pipeline,
/// these would route to a dead-letter queue for investigation.
/// </summary>
public sealed class LogParseException : Exception
{
    public int LineNumber { get; }
    public string RawLine { get; }

    public LogParseException(int lineNumber, string rawLine, string message)
        : base(message)
    {
        LineNumber = lineNumber;
        RawLine = rawLine;
    }

    public LogParseException(int lineNumber, string rawLine, string message, Exception inner)
        : base(message, inner)
    {
        LineNumber = lineNumber;
        RawLine = rawLine;
    }
}
