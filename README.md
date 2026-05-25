# Log Parser — HTTP Access Log Analyzer

A C# (.NET 8) command-line tool that parses Apache/Nginx Combined Log Format files and reports on their contents.

## Quick Start

```bash
# Build the solution
dotnet build

# Run against the sample data
dotnet run --project src/LogParser -- data/programming-task-example-data.log

# Run with verbose logging
dotnet run --project src/LogParser -- data/programming-task-example-data.log --verbose

# Run tests
dotnet run --project tests/LogParser.Tests
```

## Output

For the provided sample data, the tool reports:

| Metric | Result |
|--------|--------|
| Unique IP addresses | **11** |
| Top URL #1 | `/docs/manage-websites/` (2 requests) |
| Top URL #2 | `/faq/` (2 requests) |
| Top URL #3 | `/` (1 request) |
| Top IP #1 | `168.41.191.40` (4 requests) |
| Top IP #2 | `177.71.128.21` (3 requests) |
| Top IP #3 | `50.112.00.11` (3 requests) |

## CLI Options

```
Usage: LogParser <logfile> [--top-n N] [--verbose]

Arguments:
  logfile       Path to the log file to analyze

Options:
  --top-n N     Number of top URLs/IPs to display (default: 3)
  --verbose     Enable debug-level logging
```

## Project Structure

```
log-parser-csharp/
├── LogParser.sln
├── global.json                 # Pins SDK version (see "Why global.json" below)
├── src/LogParser/
│   ├── Models/
│   │   ├── LogEntry.cs         # Immutable record for parsed log lines
│   │   ├── AnalysisResult.cs   # Immutable record for analysis output
│   │   └── LogParseException.cs # Structured exception with line context
│   ├── Interfaces/
│   │   ├── ILogParser.cs       # Parse contract (IAsyncEnumerable streaming)
│   │   ├── ILogAnalyzer.cs     # Analysis contract
│   │   ├── IReporter.cs        # Output contract
│   │   └── IAppLogger.cs       # Logging abstraction (see "Logging" below)
│   ├── Services/
│   │   ├── AccessLogParser.cs  # Regex-based parser with URL normalization
│   │   ├── LogAnalyzer.cs      # Single-pass counting with Dictionary/HashSet
│   │   └── ConsoleReporter.cs  # Formatted console output
│   └── Program.cs              # CLI entry point (top-level statements)
├── tests/LogParser.Tests/
│   ├── TestFramework.cs        # Lightweight test runner (see "Testing" below)
│   ├── ParserTests.cs          # 17 tests — parsing, normalization, edge cases
│   ├── AnalyzerTests.cs        # 10 tests — counting, ranking, boundaries
│   └── IntegrationTests.cs     # 9 tests — full pipeline against sample data
└── data/
    └── programming-task-example-data.log
```

---

## Design Decisions

### Language Choice: C#

Evaluated Python and C# across six axes. C# was chosen because:

- **Strongest expertise**: Deepest hands-on experience with C#/.NET means I can speak authoritatively about every design decision, from the regex compilation to the async streaming pattern.
- **Scalability ceiling**: C# has native advantages for the production evolution of this tool — true multi-threaded parallelism (no GIL), `Span<T>` for zero-allocation parsing, `IAsyncEnumerable` for backpressure-aware streaming.
- **Type safety**: Records, nullable reference types, and the type system catch entire classes of bugs at compile time. The `LogEntry` record guarantees immutability — once parsed, data cannot be accidentally mutated downstream.
- **Enterprise alignment**: Mantel operates in an enterprise consulting context. C#/.NET is the dominant backend language in that space, and this solution demonstrates patterns (interface-based design, structured logging, DI-ready architecture) that translate directly to client work.

Where Python would win: faster to prototype for this specific scope, less ceremony for a 23-line file. We documented the comparison during planning and chose C# deliberately, not by default.

### Architecture: Parse → Analyze → Report

Three clean stages with zero coupling, connected through interfaces:

1. **Parser** (`ILogParser` → `AccessLogParser`) — Takes raw text, yields `LogEntry` records via `IAsyncEnumerable`. Knows about regex and log format. Knows nothing about counting.
2. **Analyzer** (`ILogAnalyzer` → `LogAnalyzer`) — Takes any `IAsyncEnumerable<LogEntry>` source, returns an `AnalysisResult`. Knows about counting. Knows nothing about log format or output.
3. **Reporter** (`IReporter` → `ConsoleReporter`) — Takes an `AnalysisResult`, produces output. Knows about formatting. Knows nothing about parsing or counting.

Each module can be tested in isolation and swapped independently. Adding a JSON reporter requires zero changes to the parser or analyzer.

### Key Assumptions

1. **All HTTP status codes are counted.** A 404 or 500 is still a request to that URL. The task says "most visited" — a visit is a request, not a successful response. This is documented and configurable by filtering in the analyzer.

2. **URL normalization strips scheme + host.** Some log entries use absolute URLs (`GET http://example.net/faq/`), others use relative paths (`GET /faq/`). These refer to the same resource. We normalize to path-only to ensure consistent counting. This is a real-world proxy log scenario.

3. **Trailing data after user-agent is ignored.** Some lines have extra fields (`junk extra`, `456 789`). This is common when load balancers or reverse proxies append metadata. The regex tolerates this gracefully.

4. **Malformed lines are logged and skipped, not fatal.** Real log files can have corrupted lines. The parser logs a structured warning with line number and content, then continues. In a cloud pipeline, these would route to a dead-letter queue via the `LogParseException`.

5. **Case-insensitive IP and URL matching.** Dictionary and HashSet use `StringComparer.OrdinalIgnoreCase` for robustness.

### Zero Runtime Dependencies

The solution uses only the .NET 8 Base Class Library — no NuGet packages required at runtime. This was a deliberate choice:

- **Portability**: Clone, `dotnet build`, run. No `dotnet restore` failures due to private NuGet feeds or corporate proxy issues.
- **Auditability**: Every line of code is in the repo. No transitive dependency surprises.
- **Interview clarity**: Nothing hidden behind a framework. Every pattern is visible and explainable.

The logging abstraction (`IAppLogger<T>`) mirrors `Microsoft.Extensions.Logging.ILogger<T>` method signatures intentionally. Migration to the real MEL package is a mechanical swap — see "Future: Cloud-Ready Logging" below.

---

## Testing

**36 tests** across three suites:

- **ParserTests (17 tests)**: URL normalization (5), line parsing (10) — covers anonymous/authenticated users, all status codes, absolute URLs, trailing data, empty/malformed input, record immutability, timestamp parsing.
- **AnalyzerTests (10 tests)**: Empty input, single entry, unique IP counting, URL/IP ranking by count, secondary alphabetical sort for ties, top-N edge cases, status distribution, invalid argument handling.
- **IntegrationTests (9 tests)**: Full pipeline against sample data with manually verified expected values, console reporter output validation, missing file handling, empty file, blank line tolerance.

The test framework is a lightweight custom runner (see `TestFramework.cs`). In a team project, this would be xUnit + FluentAssertions via NuGet. The custom runner was used here to maintain zero external dependencies while still providing proper test isolation, async support, and clear pass/fail reporting.

To migrate to xUnit:
1. Replace the test `.csproj` with standard xUnit package references
2. Replace `[Test]` attributes with `[Fact]`
3. Replace `Assert.Equal/True/Null` with xUnit equivalents (same API signatures)
4. Remove `TestFramework.cs` and `Program.cs` — xUnit provides its own runner

---

## Why .NET 8 (Not .NET 10)

.NET 10 shipped in November 2025 as the next LTS release. We evaluated it and chose .NET 8 for this submission:

**Pragmatic reasons:**
- .NET 8 is the LTS that's currently deployed in most production environments. The reviewer can build and run this with any .NET 8+ SDK.
- .NET 10 requires Visual Studio 2026 for IDE support — Visual Studio 2022 cannot target it. This creates a tooling barrier for evaluation.
- The `global.json` pins the SDK version so the project builds deterministically in mixed-SDK environments.

**What .NET 10 / C# 14 would add (documented, not needed now):**
- **JIT improvements**: .NET 10's JIT optimizes interface dispatch and eliminates bounds checks on Span access more aggressively. Our interface-based pipeline would get faster for free just by retargeting.
- **Span implicit conversions**: C# 14 makes `Span<T>` and `ReadOnlySpan<T>` first-class, with implicit conversions and extension method support. This cleans up the syntax for the zero-allocation parser path described below.
- **`field` keyword**: Eliminates explicit backing fields in properties. Minor but reduces boilerplate.
- **GC improvements**: Stack allocation of small immutable arrays, reduced GC pauses. Benefits the Dictionary/HashSet allocations in the analyzer.

**Upgrade path (one line):**
```xml
<!-- global.json or .csproj -->
<TargetFramework>net10.0</TargetFramework>
```
No code changes needed for the runtime performance gains. The Span-based parser refactor is a separate optimization pass.

### Why global.json Matters

When multiple .NET SDKs are installed side-by-side, the CLI defaults to the highest version. Without `global.json`, running `dotnet build` on a .NET 8 project with .NET 10 installed may silently use the .NET 10 SDK. This can produce subtle behavioral differences, especially in CI/CD pipelines. The `global.json` with `rollForward: latestFeature` pins to .NET 8.x while allowing patch updates.

---

## Future State: Scaling This Solution

The current implementation is correct and clean for the task scope. Below is how each component evolves for production workloads with multi-GB log files and cloud deployment. The interface-based architecture means each upgrade is isolated — you can ship them independently.

### 1. Parallel Processing (C#'s Key Advantage Over Python)

C# has no GIL. True multi-threaded parallelism works out of the box:

```csharp
// Chunked parallel processing with Parallel.ForEachAsync
await Parallel.ForEachAsync(
    FileChunker.SplitByByteOffset(filePath, chunkCount: Environment.ProcessorCount),
    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
    async (chunk, ct) =>
    {
        var localCounts = new Dictionary<string, int>();
        await foreach (var entry in parser.ParseChunkAsync(chunk, ct))
            localCounts[entry.Url] = localCounts.GetValueOrDefault(entry.Url) + 1;

        // Merge into concurrent collection
        MergeInto(globalCounts, localCounts);
    });
```

The current `ILogParser` interface already returns `IAsyncEnumerable<LogEntry>`, so the parallel reader just needs to implement the same interface with chunk-aware I/O. The analyzer and reporter are untouched.

### 2. Zero-Allocation Parsing with Span<T>

For multi-GB files, string allocations from `Substring` and regex groups become the bottleneck. The future path:

```csharp
// Current: allocates a new string per field
var ip = match.Groups[1].Value;  // heap allocation

// Future: Span-based slicing — zero allocation
ReadOnlySpan<char> line = rawLine.AsSpan();
ReadOnlySpan<char> ip = line[..line.IndexOf(' ')];  // stack-only view
```

Combined with `System.IO.Pipelines` for I/O, this reduces GC pressure to near-zero. .NET 10's improved Span support and bounds-check elimination make this pattern even more efficient.

### 3. Memory-Mapped I/O

For files larger than available RAM:

```csharp
using var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open);
using var accessor = mmf.CreateViewAccessor();
// OS handles paging — process the file as if it's in memory
```

### 4. Cloud-Ready Logging

The `IAppLogger<T>` abstraction mirrors `Microsoft.Extensions.Logging.ILogger<T>`. Migration:

```csharp
// Step 1: Add NuGet package
// Microsoft.Extensions.Logging + provider of choice (Serilog, App Insights, etc.)

// Step 2: Register in DI
services.AddSingleton(typeof(IAppLogger<>), typeof(MicrosoftLoggerAdapter<>));
// OR replace IAppLogger with ILogger directly

// Step 3: Configure providers
builder.Logging.AddApplicationInsights();  // Azure
builder.Logging.AddAWSProvider();          // AWS CloudWatch
```

The structured warning messages (`"Failed to parse line {LineNumber}: {RawLine}"`) already use the semantic logging format that Serilog and Application Insights index on. No message rewrites needed.

### 5. DI Container Integration

The current `Program.cs` uses manual constructor injection. For a hosted service:

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<ILogParser, AccessLogParser>();
builder.Services.AddSingleton<ILogAnalyzer, LogAnalyzer>();
builder.Services.AddSingleton<IReporter, ConsoleReporter>();
// OR: services.AddSingleton<IReporter, JsonReporter>(); // swap output format
```

### 6. Approximate Counting for Extreme Scale

For billions of unique IPs where exact counting exceeds memory:
- **HyperLogLog**: ~2% error margin, constant memory (~12KB) regardless of cardinality
- **Count-Min Sketch**: Probabilistic frequency counting for top-N with bounded over-count error

These would implement `ILogAnalyzer` with the same interface — the rest of the pipeline is unaware.

---

## Test Coverage

| Suite | Count | Covers |
|-------|-------|--------|
| ParserTests | 17 | URL normalization, line parsing, edge cases, immutability |
| AnalyzerTests | 10 | Counting, ranking, boundaries, tied counts, invalid input |
| IntegrationTests | 9 | Full pipeline, sample data verification, error handling |
| **Total** | **36** | |

---

## AI Usage Disclosure

This solution was developed with AI assistance (Claude). The AI was used for:

- **Planning**: Language comparison (Python vs C#), architecture decisions, .NET 8 vs .NET 10 evaluation, edge case identification from the sample data.
- **Implementation**: Code generation with iterative review, compilation verification, and test-driven fixes.
- **Testing**: Test case design including edge cases identified during data analysis (absolute URLs, trailing junk, authenticated users, status code variants).

All code has been reviewed, understood, and can be discussed in detail. The design decisions — URL normalization strategy, interface-based pipeline, async streaming, zero-dependency approach, custom logging abstraction — reflect deliberate engineering choices informed by production experience, not AI defaults.
