using LogParser.Models;

namespace LogParser.Interfaces;

/// <summary>
/// Contract for rendering analysis results. Console today,
/// HTML/JSON/CloudWatch tomorrow — swap via DI without touching
/// the parser or analyzer.
/// </summary>
public interface IReporter
{
    Task ReportAsync(AnalysisResult result, CancellationToken cancellationToken = default);
}
