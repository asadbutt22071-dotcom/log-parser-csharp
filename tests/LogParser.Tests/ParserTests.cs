using Microsoft.Extensions.Logging.Abstractions;
using LogParser.Services;
using Xunit;

namespace LogParser.Tests;

public class ParserTests
{
    private readonly AccessLogParser _parser = new(NullLogger<AccessLogParser>.Instance);

    // --- URL Normalization ---

    [Fact]
    public void Test_NormalizeUrl_RelativePath_Unchanged()
    {
        Assert.Equal("/faq/", AccessLogParser.NormalizeUrl("/faq/"));
    }

    [Fact]
    public void Test_NormalizeUrl_AbsoluteUrl_StripsSchemeAndHost()
    {
        Assert.Equal("/faq/", AccessLogParser.NormalizeUrl("http://example.net/faq/"));
    }

    [Fact]
    public void Test_NormalizeUrl_HttpsAbsoluteUrl_StripsSchemeAndHost()
    {
        Assert.Equal("/blog/", AccessLogParser.NormalizeUrl("https://example.com/blog/"));
    }

    [Fact]
    public void Test_NormalizeUrl_AbsoluteUrlWithPath_PreservesFullPath()
    {
        Assert.Equal(
            "/blog/category/meta/",
            AccessLogParser.NormalizeUrl("http://example.net/blog/category/meta/"));
    }

    [Fact]
    public void Test_NormalizeUrl_RootPath_Unchanged()
    {
        Assert.Equal("/", AccessLogParser.NormalizeUrl("/"));
    }

    // --- Line Parsing ---

    [Fact]
    public void Test_ParseLine_StandardLine_AllFieldsExtracted()
    {
        var line = @"177.71.128.21 - - [10/Jul/2018:22:21:28 +0200] ""GET /intranet-analytics/ HTTP/1.1"" 200 3574 ""-"" ""Mozilla/5.0""";
        var entry = _parser.ParseLine(line);

        Assert.NotNull(entry);
        Assert.Equal("177.71.128.21", entry!.IpAddress);
        Assert.Equal("-", entry.Identity);
        Assert.Equal("-", entry.User);
        Assert.Equal("GET", entry.Method);
        Assert.Equal("/intranet-analytics/", entry.Url);
        Assert.Equal("HTTP/1.1", entry.Protocol);
        Assert.Equal(200, entry.StatusCode);
        Assert.Equal(3574, entry.ResponseSize);
    }

    [Fact]
    public void Test_ParseLine_AuthenticatedUser_UserFieldExtracted()
    {
        var line = @"50.112.00.11 - admin [11/Jul/2018:17:31:56 +0200] ""GET /asset.js HTTP/1.1"" 200 3574 ""-"" ""Mozilla/5.0""";
        var entry = _parser.ParseLine(line);

        Assert.NotNull(entry);
        Assert.Equal("admin", entry!.User);
    }

    [Fact]
    public void Test_ParseLine_404Status_Parsed()
    {
        var line = @"168.41.191.41 - - [11/Jul/2018:17:41:30 +0200] ""GET /this/page/does/not/exist/ HTTP/1.1"" 404 3574 ""-"" ""Mozilla/5.0""";
        var entry = _parser.ParseLine(line);

        Assert.NotNull(entry);
        Assert.Equal(404, entry!.StatusCode);
        Assert.Equal("/this/page/does/not/exist/", entry.Url);
    }

    [Fact]
    public void Test_ParseLine_500Status_Parsed()
    {
        var line = @"72.44.32.11 - - [11/Jul/2018:17:42:07 +0200] ""GET /to-an-error HTTP/1.1"" 500 3574 ""-"" ""Mozilla/5.0""";
        var entry = _parser.ParseLine(line);

        Assert.NotNull(entry);
        Assert.Equal(500, entry!.StatusCode);
    }

    [Fact]
    public void Test_ParseLine_301Redirect_Parsed()
    {
        var line = @"168.41.191.43 - - [11/Jul/2018:17:43:40 +0200] ""GET /moved-permanently HTTP/1.1"" 301 3574 ""-"" ""Mozilla/5.0""";
        var entry = _parser.ParseLine(line);

        Assert.NotNull(entry);
        Assert.Equal(301, entry!.StatusCode);
    }

    [Fact]
    public void Test_ParseLine_AbsoluteUrl_NormalizedToPath()
    {
        var line = @"168.41.191.40 - - [09/Jul/2018:10:11:30 +0200] ""GET http://example.net/faq/ HTTP/1.1"" 200 3574 ""-"" ""Mozilla/5.0""";
        var entry = _parser.ParseLine(line);

        Assert.NotNull(entry);
        Assert.Equal("/faq/", entry!.Url);
    }

    [Fact]
    public void Test_ParseLine_TrailingData_Tolerated()
    {
        // Some lines in the sample have extra fields after user-agent
        var line = @"72.44.32.10 - - [09/Jul/2018:15:48:07 +0200] ""GET / HTTP/1.1"" 200 3574 ""-"" ""Mozilla/5.0 (compatible; MSIE 10.6)"" junk extra";
        var entry = _parser.ParseLine(line);

        Assert.NotNull(entry);
        Assert.Equal("/", entry!.Url);
        Assert.Equal("72.44.32.10", entry.IpAddress);
    }

    [Fact]
    public void Test_ParseLine_EmptyString_ReturnsNull()
    {
        Assert.Null(_parser.ParseLine(""));
    }

    [Fact]
    public void Test_ParseLine_Whitespace_ReturnsNull()
    {
        Assert.Null(_parser.ParseLine("   "));
    }

    [Fact]
    public void Test_ParseLine_Malformed_ReturnsNull()
    {
        Assert.Null(_parser.ParseLine("this is not a log line at all"));
    }

    [Fact]
    public void Test_ParseLine_RecordImmutability()
    {
        // Records are immutable by default — verify the entry can't be modified
        var line = @"177.71.128.21 - - [10/Jul/2018:22:21:28 +0200] ""GET /test/ HTTP/1.1"" 200 3574 ""-"" ""Mozilla/5.0""";
        var entry = _parser.ParseLine(line);
        Assert.NotNull(entry);

        // Records support with-expressions for creating modified copies
        var modified = entry! with { StatusCode = 404 };
        Assert.Equal(200, entry.StatusCode);  // Original unchanged
        Assert.Equal(404, modified.StatusCode);
    }

    [Fact]
    public void Test_ParseLine_TimestampParsedCorrectly()
    {
        var line = @"177.71.128.21 - - [10/Jul/2018:22:21:28 +0200] ""GET /test/ HTTP/1.1"" 200 3574 ""-"" ""Mozilla/5.0""";
        var entry = _parser.ParseLine(line);

        Assert.NotNull(entry);
        Assert.Equal(2018, entry!.Timestamp.Year);
        Assert.Equal(7, entry.Timestamp.Month);
        Assert.Equal(10, entry.Timestamp.Day);
        Assert.Equal(22, entry.Timestamp.Hour);
    }
}
