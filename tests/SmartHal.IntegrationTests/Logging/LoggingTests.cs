using System.Diagnostics;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartHal.IntegrationTests.Hosting;
using SmartHal.Server.Diagnostics;
using Xunit;

namespace SmartHal.IntegrationTests.Logging;

/// <summary>
/// Drives the server host and the built server process and verifies the logging setup: the two
/// sinks, the per-component minimum level and the trace correlation (AC-14, AC-15, AC-16, FR-29 to
/// FR-33, NFR-5, NFR-6).
/// </summary>
/// <remarks>
/// Serilog's own effect is read back from the rolling file that <see cref="ServerHostHarness"/>
/// redirects into its temporary directory, and the console format from a child process; no test
/// touches a Serilog type (C-6).
/// </remarks>
[Collection("ProcessEnvironment")]
public sealed class LoggingTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"smarthal-logging-{Guid.NewGuid():N}");

    public LoggingTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task GivenMinimumLevelOverrideDebugForOneComponent_WhenHostRuns_ThenOnlyThatComponentsDebugEventsAppear()
    {
        // Arrange
        await using var harness = await ServerHostHarness.StartAsync(
            options =>
            {
                options.Configuration["Serilog:MinimumLevel:Default"] = "Warning";
                options.Configuration["Serilog:MinimumLevel:Override:SmartHal.IntegrationTests.Logging"] = "Debug";
            },
            TestContext.Current.CancellationToken);

        harness.Started.Should().BeTrue();
        var loggerFactory = harness.Services.GetRequiredService<ILoggerFactory>();

        // Act
        loggerFactory.CreateLogger<LoggingTests>().LogDebug("Debug event of the overridden component.");
        loggerFactory.CreateLogger<ServerHostHarness>().LogInformation("Information event of another component.");
        await harness.StopAsync();

        // Assert
        var messageTemplates = harness.ReadSerilogJsonLog().Select(MessageTemplateOf).ToArray();
        messageTemplates.Should().Contain(
            "Debug event of the overridden component.",
            "the override raises this component to Debug without a rebuild (AC-14, FR-32)");
        messageTemplates.Should().NotContain(
            "Information event of another component.",
            "the default minimum level Warning suppresses every other component");
    }

    [Fact]
    public async Task GivenLogWrittenInsideActivity_WhenHostRuns_ThenJsonEventCarriesTheSameTraceId()
    {
        // Arrange
        await using var harness = await ServerHostHarness.StartAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        harness.Started.Should().BeTrue();
        var logger = harness.Services.GetRequiredService<ILoggerFactory>().CreateLogger<LoggingTests>();

        // Act
        using var activity = new Activity("test").Start();
        var traceId = activity.TraceId.ToString();
        logger.LogInformation("Event written inside an activity.");
        activity.Stop();
        await harness.StopAsync();

        // Assert
        var logEvent = SingleEventWith(harness, "Event written inside an activity.");
        logEvent.TryGetProperty("@tr", out var writtenTraceId).Should().BeTrue(
            "every event carries the TraceId of the running activity (AC-15, FR-33, FR-36)");
        writtenTraceId.GetString().Should().Be(traceId);
    }

    [Fact]
    public async Task GivenLogWrittenOutsideActivity_WhenHostRuns_ThenJsonEventCarriesNoTraceId()
    {
        // Arrange
        await using var harness = await ServerHostHarness.StartAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        harness.Started.Should().BeTrue();
        var logger = harness.Services.GetRequiredService<ILoggerFactory>().CreateLogger<LoggingTests>();
        var ambientActivity = Activity.Current;

        // Act
        Activity.Current = null;

        try
        {
            logger.LogInformation("Event written outside an activity.");
        }
        finally
        {
            Activity.Current = ambientActivity;
        }

        await harness.StopAsync();

        // Assert
        var logEvent = SingleEventWith(harness, "Event written outside an activity.");
        logEvent.TryGetProperty("@tr", out _).Should().BeFalse(
            "FR-33 attaches the TraceId only while an activity runs");
    }

    [Fact]
    public async Task GivenDefaultConfigurationInProduction_WhenServerProcessRuns_ThenConsoleIsJsonAndLogFileExistsUnderLogs()
    {
        // Arrange
        // The host takes its content root from the working directory, so the shipped appsettings.json
        // is copied next to it; the file itself stays unchanged, which is what "default values" means.
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            Path.Combine(_tempDirectory, "appsettings.json"));

        var options = new ProcessRunOptions
        {
            WorkingDirectory = _tempDirectory,
            StopWhenStdOutMatches = IsReadyEvent
        };

        options.EnvironmentVariables["DOTNET_ENVIRONMENT"] = "Production";
        options.EnvironmentVariables["SMARTHAL_CONFIG_FILE"] = null;
        options.EnvironmentVariables["SMARTHAL_SmartHal__InstanceName"] = "home";
        options.EnvironmentVariables["SMARTHAL_SmartHal__DataDirectory"] = Path.Combine(_tempDirectory, "data");

        // Act
        var result = await ServerProcessRunner.RunAsync(options);

        // Assert
        result.StdErr.Should().BeEmpty();
        result.StdOut.Should().NotBeEmpty("the host logs its start to the console");

        foreach (var line in result.StdOut)
        {
            var logEvent = ParseOrNull(line);
            logEvent.Should().NotBeNull(
                $"NFR-6 makes every console line outside Development compact JSON, but '{line}' is not");
            logEvent.Value.TryGetProperty("@t", out _).Should().BeTrue();
            logEvent.Value.TryGetProperty("@mt", out _).Should().BeTrue();
        }

        var logDirectory = Path.Combine(_tempDirectory, "logs");
        Directory.Exists(logDirectory).Should().BeTrue("FR-30 puts the rolling file under ./logs");
        Directory.GetFiles(logDirectory, "*.log").Should().NotBeEmpty();
    }

    [Fact]
    public void AppSettings_SerilogSection_ConfiguresJsonConsoleInProductionAndTextInDevelopment()
    {
        // Arrange
        var production = ReadAppSettings("appsettings.json");
        var development = ReadAppSettings("appsettings.Development.json");

        // Act
        var productionFormatter = production["Serilog:WriteTo:Console:Args:formatter:type"];
        var developmentFormatterType = development["Serilog:WriteTo:Console:Args:formatter:type"];
        var developmentOutputTemplate = development["Serilog:WriteTo:Console:Args:formatter:outputTemplate"];

        // Act & Assert
        productionFormatter.Should().Be(
            "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact",
            "outside Development the console sink writes compact JSON (FR-29, NFR-6)");
        developmentFormatterType.Should().Be(
            "Serilog.Formatting.Display.MessageTemplateTextFormatter, Serilog",
            "in Development the console sink writes readable text (FR-29, G-18)");
        developmentOutputTemplate.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void AppSettings_SerilogFileSink_PathIsUnderLogsAndIndependentOfDataDirectory()
    {
        // Arrange
        var production = ReadAppSettings("appsettings.json");

        // Act
        var path = production["Serilog:WriteTo:File:Args:path"];
        var smartHalKeys = production.GetSection("SmartHal").AsEnumerable(makePathsRelative: true)
            .Select(entry => entry.Key)
            .ToArray();

        // Assert
        path.Should().StartWith("./logs", "FR-30 fixes the default log directory to ./logs");
        smartHalKeys.Should().NotContain(
            key => key.Contains("log", StringComparison.OrdinalIgnoreCase),
            "FR-30 keeps the log path out of the SmartHal section, independent of DataDirectory");
        path.Should().NotContain("DataDirectory");
    }

    [Fact]
    public void AppSettings_SerilogFileSink_RollsDailyAndRetainsFourteenFiles()
    {
        // Arrange
        var production = ReadAppSettings("appsettings.json");

        // Act
        var rollingInterval = production["Serilog:WriteTo:File:Args:rollingInterval"];
        var retainedFileCountLimit = production["Serilog:WriteTo:File:Args:retainedFileCountLimit"];

        // Assert
        rollingInterval.Should().Be("Day", "NFR-5 rolls the file daily (FR-31)");
        retainedFileCountLimit.Should().Be("14", "NFR-5 keeps fourteen files (FR-31)");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static IConfigurationRoot ReadAppSettings(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, fileName);
        File.Exists(path).Should().BeTrue($"'{fileName}' must reach the output directory");

        return new ConfigurationBuilder().AddJsonFile(path, optional: false, reloadOnChange: false).Build();
    }

    private static JsonElement SingleEventWith(ServerHostHarness harness, string messageTemplate)
    {
        var matches = harness.ReadSerilogJsonLog()
            .Where(logEvent => MessageTemplateOf(logEvent) == messageTemplate)
            .ToArray();

        matches.Should().ContainSingle($"the event '{messageTemplate}' is written exactly once");

        return matches[0];
    }

    private static string? MessageTemplateOf(JsonElement logEvent)
    {
        return logEvent.TryGetProperty("@mt", out var messageTemplate) ? messageTemplate.GetString() : null;
    }

    private static bool IsReadyEvent(string line)
    {
        var logEvent = ParseOrNull(line);

        if (logEvent is null)
        {
            return false;
        }

        // Health event 1201 marks readiness and is the last event of a successful start, so the run
        // stops there rather than at the earlier lifecycle event 1001.
        return EventIdOf(logEvent.Value) == LogEvents.Health.HealthReady;
    }

    private static int? EventIdOf(JsonElement logEvent)
    {
        if (!logEvent.TryGetProperty("EventId", out var eventId))
        {
            return null;
        }

        return eventId.ValueKind switch
        {
            JsonValueKind.Number => eventId.GetInt32(),
            JsonValueKind.Object when eventId.TryGetProperty("Id", out var id)
                && id.ValueKind == JsonValueKind.Number => id.GetInt32(),
            _ => null
        };
    }

    private static JsonElement? ParseOrNull(string line)
    {
        // A line that is not JSON is exactly what the assertion is looking for, so the parse failure
        // is a result here, not an error.
        try
        {
            using var document = JsonDocument.Parse(line);

            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
