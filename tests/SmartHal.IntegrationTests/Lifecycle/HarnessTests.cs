using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmartHal.IntegrationTests.Hosting;
using SmartHal.Server.Composition;
using SmartHal.Server.Configuration;
using SmartHal.Server.Diagnostics;
using Xunit;

namespace SmartHal.IntegrationTests.Lifecycle;

/// <summary>
/// Verifies that the host fixture of <c>SmartHal.IntegrationTests</c> is complete: a test overrides
/// the configuration, waits for readiness, reads the log events, shuts down and reads the exit code,
/// and writes no start-up code of its own (AC-32, FR-63, FR-64).
/// </summary>
/// <remarks>
/// The test is written the way a following slice uses the fixture, so it doubles as the example the
/// README of this project points at. The harness sets process-wide environment variables, so it runs
/// in the serialised <c>ProcessEnvironment</c> collection.
/// </remarks>
[Collection("ProcessEnvironment")]
public sealed class HarnessTests
{
    [Fact]
    public async Task GivenHarnessWithOverriddenConfiguration_WhenUsed_ThenItWaitsForReadyCollectsLogsStopsAndExposesExitCode()
    {
        // Arrange
        await using var harness = await ServerHostHarness.StartAsync(
            options => options.Configuration["SmartHal:InstanceName"] = "harness-example",
            TestContext.Current.CancellationToken);

        // Act
        await harness.WaitForReadyAsync();

        // Assert
        harness.Started.Should().BeTrue("the fixture starts the real host in process");
        harness.Services.GetRequiredService<IOptions<SmartHalOptions>>().Value.InstanceName
            .Should().Be("harness-example", "FR-63 lets a test override the configuration");

        var eventIds = harness.Logs.GetSnapshot().Select(record => record.Id.Id).ToArray();
        eventIds.Should().Contain(LogEvents.Lifecycle.HostStarting, "FR-63 collects the log events of the host");
        eventIds.Should().Contain(LogEvents.Lifecycle.HostStarted);
        eventIds.Should().Contain(LogEvents.Health.HealthReady, "the fixture waits for the readiness signal");

        (await harness.StopAsync()).Should().Be(
            ExitCodes.Success,
            "FR-63 exposes the exit code of the orderly shutdown");
        harness.Stopped.Should().BeTrue("the fixture shuts the host down in an orderly fashion");
        harness.Completion.IsCompleted.Should().BeTrue("the start result is available once the host has ended");
        (await harness.Completion).Should().Be(ExitCodes.Success);
    }
}
