using Microsoft.Extensions.Logging.Abstractions;
using LogParser.Models;
using LogParser.Services;
using Xunit;

namespace LogParser.Tests;

public class AnalyzerTests
{
    private readonly LogAnalyzer _analyzer = new(NullLogger<LogAnalyzer>.Instance);

    private static async IAsyncEnumerable<LogEntry> ToAsyncEnumerable(params LogEntry[] entries)
    {
        foreach (var entry in entries)
            yield return entry;
        await Task.CompletedTask;
    }

    private static LogEntry MakeEntry(string ip = "1.1.1.1", string url = "/test/", int status = 200)
        => new(ip, "-", "-", DateTimeOffset.Now, "GET", url, "HTTP/1.1", status, 3574, "TestAgent");

    [Fact]
    public async Task Test_Analyze_EmptyStream_ReturnsZeroes()
    {
        var result = await _analyzer.AnalyzeAsync(ToAsyncEnumerable());

        Assert.Equal(0, result.UniqueIpCount);
        Assert.Equal(0, result.TotalRequests);
        Assert.Empty(result.TopUrls);
        Assert.Empty(result.TopIpAddresses);
    }

    [Fact]
    public async Task Test_Analyze_SingleEntry_CountsCorrectly()
    {
        var result = await _analyzer.AnalyzeAsync(
            ToAsyncEnumerable(MakeEntry("10.0.0.1", "/home/")));

        Assert.Equal(1, result.UniqueIpCount);
        Assert.Equal(1, result.TotalRequests);
        Assert.Equal("/home/", result.TopUrls[0].Url);
        Assert.Equal(1, result.TopUrls[0].Count);
    }

    [Fact]
    public async Task Test_Analyze_UniqueIpCounting()
    {
        var result = await _analyzer.AnalyzeAsync(ToAsyncEnumerable(
            MakeEntry("10.0.0.1"),
            MakeEntry("10.0.0.2"),
            MakeEntry("10.0.0.1"),  // duplicate
            MakeEntry("10.0.0.3")));

        Assert.Equal(3, result.UniqueIpCount);
        Assert.Equal(4, result.TotalRequests);
    }

    [Fact]
    public async Task Test_Analyze_UrlRanking_DescendingByCount()
    {
        var result = await _analyzer.AnalyzeAsync(ToAsyncEnumerable(
            MakeEntry(url: "/rare/"),
            MakeEntry(url: "/popular/"),
            MakeEntry(url: "/popular/"),
            MakeEntry(url: "/popular/"),
            MakeEntry(url: "/medium/"),
            MakeEntry(url: "/medium/")));

        Assert.Equal("/popular/", result.TopUrls[0].Url);
        Assert.Equal(3, result.TopUrls[0].Count);
        Assert.Equal("/medium/", result.TopUrls[1].Url);
        Assert.Equal(2, result.TopUrls[1].Count);
        Assert.Equal("/rare/", result.TopUrls[2].Url);
        Assert.Equal(1, result.TopUrls[2].Count);
    }

    [Fact]
    public async Task Test_Analyze_IpRanking_DescendingByCount()
    {
        var result = await _analyzer.AnalyzeAsync(ToAsyncEnumerable(
            MakeEntry("1.1.1.1"), MakeEntry("1.1.1.1"), MakeEntry("1.1.1.1"),
            MakeEntry("2.2.2.2"), MakeEntry("2.2.2.2"),
            MakeEntry("3.3.3.3")));

        Assert.Equal("1.1.1.1", result.TopIpAddresses[0].IpAddress);
        Assert.Equal(3, result.TopIpAddresses[0].Count);
        Assert.Equal("2.2.2.2", result.TopIpAddresses[1].IpAddress);
        Assert.Equal(2, result.TopIpAddresses[1].Count);
    }

    [Fact]
    public async Task Test_Analyze_TopN_RespectsLimit()
    {
        var result = await _analyzer.AnalyzeAsync(
            ToAsyncEnumerable(
                MakeEntry("1.1.1.1", "/a/"), MakeEntry("2.2.2.2", "/b/"),
                MakeEntry("3.3.3.3", "/c/"), MakeEntry("4.4.4.4", "/d/")),
            topN: 2);

        Assert.Equal(2, result.TopUrls.Count);
        Assert.Equal(2, result.TopIpAddresses.Count);
    }

    [Fact]
    public async Task Test_Analyze_TopN_FewerThanN_ReturnsAll()
    {
        var result = await _analyzer.AnalyzeAsync(
            ToAsyncEnumerable(MakeEntry(url: "/only-one/")),
            topN: 5);

        Assert.Single(result.TopUrls);
    }

    [Fact]
    public async Task Test_Analyze_StatusDistribution_AllStatusesCounted()
    {
        var result = await _analyzer.AnalyzeAsync(ToAsyncEnumerable(
            MakeEntry(status: 200),
            MakeEntry(status: 200),
            MakeEntry(status: 404),
            MakeEntry(status: 500),
            MakeEntry(status: 301)));

        Assert.Equal(2, result.StatusCodeDistribution[200]);
        Assert.Equal(1, result.StatusCodeDistribution[404]);
        Assert.Equal(1, result.StatusCodeDistribution[500]);
        Assert.Equal(1, result.StatusCodeDistribution[301]);
    }

    [Fact]
    public async Task Test_Analyze_InvalidTopN_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _analyzer.AnalyzeAsync(ToAsyncEnumerable(), topN: 0));
    }

    [Fact]
    public async Task Test_Analyze_TiedCounts_SecondaryAlphaSort()
    {
        // When counts are equal, results should be alphabetically sorted
        var result = await _analyzer.AnalyzeAsync(ToAsyncEnumerable(
            MakeEntry(url: "/zebra/"),
            MakeEntry(url: "/alpha/"),
            MakeEntry(url: "/middle/")));

        // All have count 1, so alpha sort: /alpha/, /middle/, /zebra/
        Assert.Equal("/alpha/", result.TopUrls[0].Url);
        Assert.Equal("/middle/", result.TopUrls[1].Url);
        Assert.Equal("/zebra/", result.TopUrls[2].Url);
    }
}
