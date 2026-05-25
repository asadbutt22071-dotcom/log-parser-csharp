using System.Text.Json;
using LogParser.Models;
using LogParser.Services;
using Xunit;

namespace LogParser.Tests;

public class JsonReporterTests
{
    private static AnalysisResult MakeResult()
    {
        var topUrls = new List<(string Url, int Count)> { ("/home/", 5), ("/about/", 2) };
        var topIps  = new List<(string IpAddress, int Count)> { ("1.1.1.1", 3), ("2.2.2.2", 1) };
        var status  = new Dictionary<int, int> { [200] = 7, [404] = 1 };
        return new AnalysisResult(2, topUrls, topIps, 8, 0, status);
    }

    [Fact]
    public async Task ReportAsync_OutputsValidJson()
    {
        using var writer = new StringWriter();
        await new JsonReporter(writer).ReportAsync(MakeResult());

        using var doc = JsonDocument.Parse(writer.ToString());
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task ReportAsync_TopLevelFieldsMatchResult()
    {
        using var writer = new StringWriter();
        await new JsonReporter(writer).ReportAsync(MakeResult());

        using var doc = JsonDocument.Parse(writer.ToString());
        var root = doc.RootElement;

        Assert.Equal(8, root.GetProperty("totalRequests").GetInt32());
        Assert.Equal(2, root.GetProperty("uniqueIpCount").GetInt32());
    }

    [Fact]
    public async Task ReportAsync_TopUrls_HaveUrlAndCountProperties()
    {
        using var writer = new StringWriter();
        await new JsonReporter(writer).ReportAsync(MakeResult());

        using var doc = JsonDocument.Parse(writer.ToString());
        var first = doc.RootElement.GetProperty("topUrls")[0];

        Assert.Equal("/home/", first.GetProperty("url").GetString());
        Assert.Equal(5, first.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task ReportAsync_TopIpAddresses_HaveIpAddressAndCountProperties()
    {
        using var writer = new StringWriter();
        await new JsonReporter(writer).ReportAsync(MakeResult());

        using var doc = JsonDocument.Parse(writer.ToString());
        var first = doc.RootElement.GetProperty("topIpAddresses")[0];

        Assert.Equal("1.1.1.1", first.GetProperty("ipAddress").GetString());
        Assert.Equal(3, first.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task ReportAsync_StatusCodes_AreSortedAscending()
    {
        using var writer = new StringWriter();
        await new JsonReporter(writer).ReportAsync(MakeResult());

        using var doc = JsonDocument.Parse(writer.ToString());
        var keys = doc.RootElement
            .GetProperty("statusCodeDistribution")
            .EnumerateObject()
            .Select(p => int.Parse(p.Name))
            .ToList();

        Assert.Equal(keys.OrderBy(k => k).ToList(), keys);
    }
}
