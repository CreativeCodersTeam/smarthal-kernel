using AwesomeAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Testing;
using SmartHal.Server.Diagnostics;
using SmartHal.Server.Health;
using Xunit;

namespace SmartHal.Server.UnitTests.Health;

/// <summary>
/// Verifies that every readiness state change is written exactly once, with the <c>EventId</c> of
/// section 6.6 (FR-42, IF-6).
/// </summary>
/// <remarks>
/// The publisher is driven directly with health reports instead of through a host, so the test
/// states which report produces which event. The logger is the <c>FakeLogger</c> of
/// <c>Microsoft.Extensions.Diagnostics.Testing</c>, because the assertion is about the
/// <c>EventId</c> of the record rather than about a message.
/// </remarks>
public sealed class HealthStateMonitorTests
{
    [Fact]
    public async Task PublishAsync_StateChanges_LogsMatchingEventIdOnce()
    {
        // Arrange
        var logger = new FakeLogger<HealthStateMonitor>();
        var monitor = new HealthStateMonitor(logger);

        // Act & Assert
        // The first report opens the signal with 1200 and, because it passes, reaches 1201.
        await monitor.PublishAsync(ReportOf(HealthStatus.Healthy), TestContext.Current.CancellationToken);
        EventIdsOf(logger).Should().Equal(
            LogEvents.Health.HealthStarting,
            LogEvents.Health.HealthReady);

        // An unchanged state is not written again.
        await monitor.PublishAsync(ReportOf(HealthStatus.Healthy), TestContext.Current.CancellationToken);
        EventIdsOf(logger).Should().Equal(
            LogEvents.Health.HealthStarting,
            LogEvents.Health.HealthReady);

        // A failing readiness check leaves the ready state, which is written once.
        await monitor.PublishAsync(ReportOf(HealthStatus.Unhealthy), TestContext.Current.CancellationToken);
        await monitor.PublishAsync(ReportOf(HealthStatus.Degraded), TestContext.Current.CancellationToken);
        EventIdsOf(logger).Should().Equal(
            LogEvents.Health.HealthStarting,
            LogEvents.Health.HealthReady,
            LogEvents.Health.HealthNotReady);

        // The way back is written again, because it is a change.
        await monitor.PublishAsync(ReportOf(HealthStatus.Healthy), TestContext.Current.CancellationToken);
        EventIdsOf(logger).Should().Equal(
            LogEvents.Health.HealthStarting,
            LogEvents.Health.HealthReady,
            LogEvents.Health.HealthNotReady,
            LogEvents.Health.HealthReady);
    }

    [Fact]
    public async Task PublishAsync_FirstReportFails_LogsStartingWithoutAStateChange()
    {
        // Arrange
        var logger = new FakeLogger<HealthStateMonitor>();
        var monitor = new HealthStateMonitor(logger);

        // Act
        await monitor.PublishAsync(ReportOf(HealthStatus.Unhealthy), TestContext.Current.CancellationToken);

        // Assert
        // FR-40 keeps the process in "starting" until a readiness check has passed for the first time.
        EventIdsOf(logger).Should().Equal(LogEvents.Health.HealthStarting);
    }

    private static HealthReport ReportOf(HealthStatus status)
    {
        var entry = new HealthReportEntry(
            status,
            description: "configuration",
            duration: TimeSpan.FromMilliseconds(1),
            exception: null,
            data: null);

        return new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal) { ["configuration"] = entry },
            status,
            TimeSpan.FromMilliseconds(1));
    }

    private static int[] EventIdsOf(FakeLogger logger)
    {
        return [.. logger.Collector.GetSnapshot().Select(record => record.Id.Id)];
    }
}
