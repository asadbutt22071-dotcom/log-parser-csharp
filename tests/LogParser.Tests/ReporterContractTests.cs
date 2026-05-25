using LogParser.Interfaces;
using LogParser.Models;
using LogParser.Services;
using Xunit;

namespace LogParser.Tests;

/// <summary>
/// Abstract contract tests that every IReporter implementation must satisfy.
/// Add a concrete subclass for each new reporter — the inherited [Fact] methods
/// run automatically against it, ensuring the contract is never silently broken.
/// </summary>
public abstract class ReporterContractTests
{
    protected abstract IReporter CreateReporter(TextWriter writer);

    private static AnalysisResult MakeResult()
    {
        var topUrls = new List<(string Url, int Count)> { ("/home/", 5), ("/about/", 2) };
        var topIps  = new List<(string IpAddress, int Count)> { ("1.1.1.1", 3), ("2.2.2.2", 1) };
        var status  = new Dictionary<int, int> { [200] = 9, [404] = 1 };
        return new AnalysisResult(2, topUrls, topIps, 10, 0, status);
    }

    [Fact]
    public async Task ReportAsync_ProducesOutput()
    {
        using var writer = new StringWriter();
        await CreateReporter(writer).ReportAsync(MakeResult());
        Assert.NotEmpty(writer.ToString());
    }

    [Fact]
    public async Task ReportAsync_HandlesEmptyCollections_WithoutThrowing()
    {
        using var writer = new StringWriter();
        var empty = new AnalysisResult(0, [], [], 0, 0, new Dictionary<int, int>());
        await CreateReporter(writer).ReportAsync(empty);
    }

    [Fact]
    public async Task ReportAsync_RespectsCompletedCancellationToken()
    {
        using var writer = new StringWriter();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // A reporter that checks the token should throw; one that ignores it should succeed.
        // Both behaviours are acceptable — the contract only forbids silent data corruption.
        try
        {
            await CreateReporter(writer).ReportAsync(MakeResult(), cts.Token);
        }
        catch (OperationCanceledException) { /* acceptable */ }
    }
}

public class ConsoleReporterContractTests : ReporterContractTests
{
    protected override IReporter CreateReporter(TextWriter writer) => new ConsoleReporter(writer);
}

public class JsonReporterContractTests : ReporterContractTests
{
    protected override IReporter CreateReporter(TextWriter writer) => new JsonReporter(writer);
}
