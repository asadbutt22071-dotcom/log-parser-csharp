using Microsoft.Extensions.Logging.Abstractions;
using LogParser.Services;
using Xunit;

namespace LogParser.Tests;

public class IntegrationTests
{
    // Path relative to where tests run from
    private static string FindDataFile()
    {
        // Walk up from bin/Debug/net8.0 to find the data directory
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(dir, "data", "programming-task-example-data.log");
            if (File.Exists(candidate))
                return candidate;
            dir = Path.GetDirectoryName(dir) ?? dir;
        }
        throw new FileNotFoundException("Could not find sample data file");
    }

    [Fact]
    public async Task Test_FullPipeline_SampleData_UniqueIpCount()
    {
        var parser = new AccessLogParser(NullLogger<AccessLogParser>.Instance);
        var analyzer = new LogAnalyzer(NullLogger<LogAnalyzer>.Instance);

        var entries = parser.ParseFileAsync(FindDataFile());
        var result = await analyzer.AnalyzeAsync(entries);

        Assert.Equal(11, result.UniqueIpCount);
    }

    [Fact]
    public async Task Test_FullPipeline_SampleData_TotalRequests()
    {
        var parser = new AccessLogParser(NullLogger<AccessLogParser>.Instance);
        var analyzer = new LogAnalyzer(NullLogger<LogAnalyzer>.Instance);

        var entries = parser.ParseFileAsync(FindDataFile());
        var result = await analyzer.AnalyzeAsync(entries);

        Assert.Equal(23, result.TotalRequests);
    }

    [Fact]
    public async Task Test_FullPipeline_SampleData_TopUrls()
    {
        var parser = new AccessLogParser(NullLogger<AccessLogParser>.Instance);
        var analyzer = new LogAnalyzer(NullLogger<LogAnalyzer>.Instance);

        var entries = parser.ParseFileAsync(FindDataFile());
        var result = await analyzer.AnalyzeAsync(entries);

        // Top URLs: /docs/manage-websites/ (2), /faq/ (2), then alpha-sorted 1-count URLs
        Assert.Equal(3, result.TopUrls.Count);
        Assert.Equal("/docs/manage-websites/", result.TopUrls[0].Url);
        Assert.Equal(2, result.TopUrls[0].Count);
        Assert.Equal("/faq/", result.TopUrls[1].Url);
        Assert.Equal(2, result.TopUrls[1].Count);
    }

    [Fact]
    public async Task Test_FullPipeline_SampleData_TopIps()
    {
        var parser = new AccessLogParser(NullLogger<AccessLogParser>.Instance);
        var analyzer = new LogAnalyzer(NullLogger<LogAnalyzer>.Instance);

        var entries = parser.ParseFileAsync(FindDataFile());
        var result = await analyzer.AnalyzeAsync(entries);

        Assert.Equal("168.41.191.40", result.TopIpAddresses[0].IpAddress);
        Assert.Equal(4, result.TopIpAddresses[0].Count);
        Assert.Equal("177.71.128.21", result.TopIpAddresses[1].IpAddress);
        Assert.Equal(3, result.TopIpAddresses[1].Count);
        Assert.Equal("50.112.00.11", result.TopIpAddresses[2].IpAddress);
        Assert.Equal(3, result.TopIpAddresses[2].Count);
    }

    [Fact]
    public async Task Test_FullPipeline_SampleData_StatusDistribution()
    {
        var parser = new AccessLogParser(NullLogger<AccessLogParser>.Instance);
        var analyzer = new LogAnalyzer(NullLogger<LogAnalyzer>.Instance);

        var entries = parser.ParseFileAsync(FindDataFile());
        var result = await analyzer.AnalyzeAsync(entries);

        Assert.Equal(19, result.StatusCodeDistribution[200]);
        Assert.Equal(1, result.StatusCodeDistribution[404]);
        Assert.Equal(1, result.StatusCodeDistribution[500]);
        Assert.Equal(1, result.StatusCodeDistribution[301]);
        Assert.Equal(1, result.StatusCodeDistribution[307]);
    }

    [Fact]
    public async Task Test_FullPipeline_MissingFile_ThrowsFileNotFound()
    {
        var parser = new AccessLogParser(NullLogger<AccessLogParser>.Instance);
        var analyzer = new LogAnalyzer(NullLogger<LogAnalyzer>.Instance);

        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            var entries = parser.ParseFileAsync("/nonexistent/path.log");
            await analyzer.AnalyzeAsync(entries);
        });
    }

    [Fact]
    public async Task Test_ConsoleReporter_ProducesOutput()
    {
        var parser = new AccessLogParser(NullLogger<AccessLogParser>.Instance);
        var analyzer = new LogAnalyzer(NullLogger<LogAnalyzer>.Instance);

        var entries = parser.ParseFileAsync(FindDataFile());
        var result = await analyzer.AnalyzeAsync(entries);

        using var writer = new StringWriter();
        var reporter = new ConsoleReporter(writer);
        await reporter.ReportAsync(result);

        var output = writer.ToString();
        Assert.Contains("Unique IP addresses", output);
        Assert.Contains("168.41.191.40", output);
        Assert.Contains("/faq/", output);
        Assert.Contains("Log Analysis Report", output);
    }

    [Fact]
    public async Task Test_FullPipeline_EmptyFile_ZeroResults()
    {
        // Create a temp empty file
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "");

            var parser = new AccessLogParser(NullLogger<AccessLogParser>.Instance);
            var analyzer = new LogAnalyzer(NullLogger<LogAnalyzer>.Instance);

            var entries = parser.ParseFileAsync(tempFile);
            var result = await analyzer.AnalyzeAsync(entries);

            Assert.Equal(0, result.TotalRequests);
            Assert.Equal(0, result.UniqueIpCount);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Test_FullPipeline_BlankLines_Skipped()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var content = @"
177.71.128.21 - - [10/Jul/2018:22:21:28 +0200] ""GET /test/ HTTP/1.1"" 200 3574 ""-"" ""Mozilla/5.0""

";
            await File.WriteAllTextAsync(tempFile, content);

            var parser = new AccessLogParser(NullLogger<AccessLogParser>.Instance);
            var analyzer = new LogAnalyzer(NullLogger<LogAnalyzer>.Instance);

            var entries = parser.ParseFileAsync(tempFile);
            var result = await analyzer.AnalyzeAsync(entries);

            Assert.Equal(1, result.TotalRequests);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
