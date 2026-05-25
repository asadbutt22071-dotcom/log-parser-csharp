# Log Parser — HTTP Access Log Analyzer

A C# (.NET 8) command-line tool that parses Apache/Nginx Combined Log Format files and reports on their contents.

## Quick Start

```bash
# Build
dotnet build

# Run against the sample data
dotnet run --project src/LogParser -- data/programming-task-example-data.log

# JSON output
dotnet run --project src/LogParser -- data/programming-task-example-data.log --format json

# Run tests
dotnet test
```

## CLI Options

```
Usage: LogParser <logfile> [--top-n N] [--format text|json] [--verbose]

Arguments:
  logfile             Path to the log file to analyze

Options:
  --top-n N           Number of top URLs/IPs to display (default: 3)
  --format text|json  Output format (default: text)
  --verbose           Enable debug-level logging
```

## Sample Output

For the provided sample data:

| Metric | Result |
|--------|--------|
| Unique IP addresses | **11** |
| Top URL #1 | `/docs/manage-websites/` (2 requests) |
| Top URL #2 | `/faq/` (2 requests) |
| Top URL #3 | `/` (1 request) |
| Top IP #1 | `168.41.191.40` (4 requests) |
| Top IP #2 | `177.71.128.21` (3 requests) |
| Top IP #3 | `50.112.00.11` (3 requests) |

## Project Structure

```
log-parser-csharp/
├── src/LogParser/
│   ├── Interfaces/
│   │   ├── ILogParser.cs       # Streaming parse contract
│   │   ├── ILogAnalyzer.cs     # Analysis contract
│   │   └── IReporter.cs        # Output contract
│   ├── Models/
│   │   ├── LogEntry.cs         # Immutable record for a parsed line
│   │   └── AnalysisResult.cs   # Immutable analysis output
│   ├── Services/
│   │   ├── AccessLogParser.cs  # Regex-based parser, streams via IAsyncEnumerable
│   │   ├── LogAnalyzer.cs      # Single-pass frequency counting
│   │   ├── ConsoleReporter.cs  # Formatted text output
│   │   └── JsonReporter.cs     # JSON output (swap via --format json)
│   └── Program.cs              # CLI entry point, DI wiring
└── tests/LogParser.Tests/
    ├── ParserTests.cs           # 17 tests — parsing, normalization, edge cases
    ├── AnalyzerTests.cs         # 10 tests — counting, ranking, boundaries
    ├── IntegrationTests.cs      # 9 tests  — full pipeline against sample data
    ├── ReporterContractTests.cs # 6 tests  — shared contract across all reporters
    └── JsonReporterTests.cs     # 5 tests  — JSON structure and field correctness
```

---

## Design Decisions

### Language: C#

C# was chosen over Python for this submission:

- **Expertise**: Deepest hands-on experience — every design decision from regex compilation to async streaming can be discussed in detail.
- **Scalability ceiling**: True multi-threaded parallelism (no GIL), `Span<T>` for zero-allocation parsing, `IAsyncEnumerable` for backpressure-aware streaming.
- **Type safety**: Records, nullable reference types, and sealed classes catch entire bug classes at compile time.
- **Enterprise fit**: Interface-based design, structured logging, and DI-ready architecture translate directly to production C# codebases.

Where Python would win: faster to prototype for this specific scope, less ceremony for a 23-line file.

### Architecture: Parse → Analyze → Report

Three decoupled stages, each behind an interface:

1. **`ILogParser` → `AccessLogParser`** — Yields `LogEntry` records via `IAsyncEnumerable`. Knows about regex and log format. Knows nothing about counting.
2. **`ILogAnalyzer` → `LogAnalyzer`** — Accepts any `IAsyncEnumerable<LogEntry>` source, returns `AnalysisResult`. Knows about counting. Knows nothing about format or output.
3. **`IReporter` → `ConsoleReporter` / `JsonReporter`** — Accepts `AnalysisResult`, produces output. Knows about formatting. Knows nothing about parsing or counting.

The DI container selects implementations at runtime — adding `JsonReporter` required zero changes to the parser or analyzer:

```csharp
services.AddSingleton<ILogParser, AccessLogParser>();
services.AddSingleton<ILogAnalyzer, LogAnalyzer>();
services.AddSingleton<IReporter>(_ => format switch
{
    "json" => new JsonReporter(),
    _      => new ConsoleReporter()
});
```

### Assumptions

1. **All HTTP status codes count as visits.** A 404 or 500 is still a request to that URL. "Most visited" means most requested, not most successfully served.

2. **URL normalization strips scheme + host.** Absolute URLs (`GET http://example.net/faq/`) and relative paths (`GET /faq/`) refer to the same resource and are normalized to path-only form before counting.

3. **Trailing data after user-agent is tolerated.** Some lines have extra fields appended by load balancers or reverse proxies. The regex ignores them gracefully.

4. **Malformed lines are logged and skipped, not fatal.** Real log files contain corrupted entries. The parser logs a structured warning with line number and truncated content, then continues streaming.

5. **Case-insensitive IP and URL matching.** All `Dictionary` and `HashSet` instances use `StringComparer.OrdinalIgnoreCase`.

---

## Path to Scalability

The interface-based pipeline means each upgrade is isolated — components can be swapped or scaled independently without touching the rest of the chain.

### Swapping the data source

`ILogParser` today reads a local file via `StreamReader`. Swapping in an S3, Kafka, or HTTP stream is a new class implementing the same interface — the analyzer and reporter are untouched:

```csharp
// Drop-in replacement — zero changes downstream
public sealed class S3LogParser : ILogParser
{
    public async IAsyncEnumerable<LogEntry> ParseFileAsync(string s3Uri, ...) { ... }
}

// One-line change in Program.cs
services.AddSingleton<ILogParser, S3LogParser>();
```

### Swapping the output format

`IReporter` is already demonstrated with two implementations (`ConsoleReporter`, `JsonReporter`). Adding HTML, CSV, or a cloud sink follows the same pattern. The shared contract tests in `ReporterContractTests` run automatically against every new implementation.

### Parallel processing

`IAsyncEnumerable` streaming already supports chunk-based parallelism. C# has no GIL — true multi-threaded processing works without any interface changes:

```csharp
await Parallel.ForEachAsync(
    FileChunker.SplitByOffset(filePath, Environment.ProcessorCount),
    async (chunk, ct) =>
    {
        await foreach (var entry in parser.ParseChunkAsync(chunk, ct))
            localCounts.Increment(entry.Url);
    });
```

### Zero-allocation parsing

For multi-GB files, `Span<T>` eliminates per-field string heap allocations:

```csharp
// Current — allocates a string per captured group
var ip = match.Groups["ip"].Value;

// Future — zero-allocation stack view
ReadOnlySpan<char> ip = line[..line.IndexOf(' ')];
```

### Approximate counting at extreme scale

For billions of unique IPs where exact counting exceeds memory, `ILogAnalyzer` can be replaced with a probabilistic implementation — **HyperLogLog** (~2% error, ~12KB memory) or **Count-Min Sketch** for top-N frequency. The rest of the pipeline is unaware.

---

## .NET 8 vs .NET 10

.NET 8 is the current LTS and the version most deployed in production. .NET 10 was evaluated and intentionally deferred:

- .NET 8 builds with any SDK ≥ 8.0 — no tooling barrier for reviewers or CI environments.
- `global.json` pins the SDK version for deterministic builds across mixed-SDK machines.

**What .NET 10 / C# 14 adds** (no code changes needed to benefit):
- More aggressive JIT optimisation of interface dispatch — the three-stage pipeline gets faster for free.
- First-class `Span<T>` implicit conversions, simplifying the zero-allocation parser path.
- GC improvements that reduce pause times on the `Dictionary`/`HashSet` allocations in the analyzer.

Upgrade path: change one line in `global.json` or the `.csproj`. No source changes required for the runtime gains.

---

## Test Coverage

| Suite | Tests | Scope |
|-------|-------|-------|
| ParserTests | 17 | URL normalization, line parsing, edge cases |
| AnalyzerTests | 10 | Counting, ranking, ties, boundaries |
| IntegrationTests | 9 | Full pipeline, sample data, error handling |
| ReporterContractTests | 6 | Shared contract across ConsoleReporter and JsonReporter |
| JsonReporterTests | 5 | JSON validity, field names, sort order |
| **Total** | **47** | |

---

## AI Usage

This solution was developed with AI assistance (Claude Code). Used for:

- **Architecture**: Validating the interface-based pipeline design and DI wiring approach against production patterns.
- **Code review**: Identifying dead code (`LogParseException`, `FailedParseCount`, redundant `HashSet`), redundant operations (`ThrowIfCancellationRequested` after `ReadLineAsync`), and improving clarity (named regex groups, `GetValueOrDefault` over `TryGetValue`).
- **Testing**: Migrating from a custom test runner to xUnit, introducing reporter contract tests, and adding the `JsonReporter` with JSON-specific assertions.

All decisions have been reviewed and can be discussed in detail.
