using System.Text.Json;
using LogParser.Interfaces;
using LogParser.Models;

namespace LogParser.Services;

public sealed class JsonReporter : IReporter
{
    private readonly TextWriter _output;
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonReporter(TextWriter? output = null)
    {
        _output = output ?? Console.Out;
    }

    public async Task ReportAsync(AnalysisResult result, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            result.TotalRequests,
            result.UniqueIpCount,
            TopUrls = result.TopUrls.Select(t => new { t.Url, t.Count }),
            TopIpAddresses = result.TopIpAddresses.Select(t => new { t.IpAddress, t.Count }),
            StatusCodeDistribution = result.StatusCodeDistribution
                .OrderBy(kv => kv.Key)
                .ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)
        };

        var json = JsonSerializer.Serialize(payload, Options);
        await _output.WriteLineAsync(json.AsMemory(), cancellationToken);
    }
}
