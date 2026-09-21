using System.Runtime.InteropServices;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SmartHal.IntegrationTests.Hosting;
using SmartHal.Server.Composition;
using SmartHal.Server.Diagnostics;
using Xunit;

namespace SmartHal.IntegrationTests.Lifecycle;

/// <summary>
/// Drives the server host in process and verifies its shutdown: both signals stop the hosted
/// services, the configured timeout bounds the shutdown, a hosted service that observes its token is
/// cancelled long before that, and the exit codes of section 6.5 hold (AC-10, AC-11, AC-12, AC-13,
/// FR-14, FR-16, FR-17, NFR-10).
/// </summary>
/// <remarks>
/// The signals are handed to <c>ShutdownSignalHandler</c> in process rather than sent to the
/// operating system, so the tests run on every platform; the real signals are a manual step (C-4).
/// The harness sets process-wide environment variables, so the tests run in the serialised
/// <c>ProcessEnvironment</c> collection.
/// </remarks>
[Collection("ProcessEnvironment")]
public sealed class ShutdownTests
{
    private const string ShutdownTimeoutKey = "SmartHal:ShutdownTimeout";

    [Fact]
    public async Task GivenReadyHost_WhenSigtermIsHandled_ThenHostedServicesStopAndExitCodeIsZero()
    {
        // Arrange
        var hostedService = new StopRecordingService();

        await using var harness = await ServerHostHarness.StartAsync(
            options => options.ConfigureServices = services => services.AddHostedService(_ => hostedService),
            TestContext.Current.CancellationToken);

        await harness.WaitForReadyAsync();

        // Act
        var exitCode = await harness.SignalAsync(PosixSignal.SIGTERM);

        // Assert
        hostedService.HasStopped.Should().BeTrue("AC-10 stops every hosted service");
        harness.Stopped.Should().BeTrue("the host stops its own hosted services as well");
        exitCode.Should().Be(ExitCodes.Success, "an orderly end returns exit code 0 (FR-17)");

        EventIdsOf(harness).Should().Contain(
            LogEvents.Lifecycle.ShutdownRequested,
            "the received signal is event 1002");
        EventIdsOf(harness).Should().Contain(
            LogEvents.Lifecycle.HostStopped,
            "the orderly end is event 1003");
    }

    [Fact]
    public async Task GivenReadyHost_WhenSigintIsHandled_ThenHostedServicesStopAndExitCodeIsZero()
    {
        // Arrange
        var hostedService = new StopRecordingService();

        await using var harness = await ServerHostHarness.StartAsync(
            options => options.ConfigureServices = services => services.AddHostedService(_ => hostedService),
            TestContext.Current.CancellationToken);

        await harness.WaitForReadyAsync();

        // Act
        var exitCode = await harness.SignalAsync(PosixSignal.SIGINT);

        // Assert
        hostedService.HasStopped.Should().BeTrue("AC-11 stops every hosted service");
        harness.Stopped.Should().BeTrue("the host stops its own hosted services as well");
        exitCode.Should().Be(ExitCodes.Success, "an orderly end returns exit code 0 (FR-17)");

        harness.Options.TerminateRecorder.ExitCodes.Should().BeEmpty(
            "a single interrupt shuts down in an orderly fashion instead of aborting (FR-15)");
    }

    [Fact]
    public async Task GivenHostedServiceExceedingShutdownTimeout_WhenShuttingDown_ThenProcessEndsAfterTimeoutWithWarning()
    {
        // Arrange
        // The hosted service ignores the token it is stopped with, so only the timeout of FR-16 can
        // end the shutdown. It is released again once the assertions are done.
        var hostedService = new UnstoppableService();

        try
        {
            await using var harness = await ServerHostHarness.StartAsync(
                options =>
                {
                    options.Configuration[ShutdownTimeoutKey] = "00:00:01";
                    options.ConfigureServices = services => services.AddHostedService(_ => hostedService);
                },
                TestContext.Current.CancellationToken);

            await harness.WaitForReadyAsync();

            // Act
            var exitCode = await harness.StopAsync()
                .WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

            // Assert
            exitCode.Should().Be(
                ExitCodes.Success,
                "an exceeded shutdown timeout is a warning rather than an error (G-17)");
            EventIdsOf(harness).Should().Contain(
                LogEvents.Lifecycle.ShutdownTimeoutExceeded,
                "FR-16 writes the aborted shutdown as a warning with event 1004");
            EventIdsOf(harness).Should().Contain(
                LogEvents.Lifecycle.HostStopped,
                "the process still ends with event 1003");
        }
        finally
        {
            hostedService.Release();
        }
    }

    [Fact]
    public async Task GivenHostedServiceObservingCancellationToken_WhenShuttingDown_ThenTokenIsCancelledBeforeTheTimeout()
    {
        // Arrange
        var hostedService = new TokenObservingService();

        await using var harness = await ServerHostHarness.StartAsync(
            options =>
            {
                options.Configuration[ShutdownTimeoutKey] = "00:00:05";
                options.ConfigureServices = services => services.AddHostedService(_ => hostedService);
            },
            TestContext.Current.CancellationToken);

        await harness.WaitForReadyAsync();

        // Act
        var completion = harness.StopAsync();

        // Assert
        // AC-13 expects the token well inside the five seconds the shutdown may take; one second is
        // the bound the plan names.
        await hostedService.StoppingTokenCancelled
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        (await completion).Should().Be(ExitCodes.Success);
    }

    [Fact]
    public async Task GivenConfiguredShutdownTimeout_WhenHostStarts_ThenHostOptionsCarryIt()
    {
        // Arrange
        await using var harness = await ServerHostHarness.StartAsync(
            options => options.Configuration[ShutdownTimeoutKey] = "00:00:07",
            TestContext.Current.CancellationToken);

        await harness.WaitForReadyAsync();

        // Act
        var hostOptions = harness.Services.GetRequiredService<IOptions<HostOptions>>().Value;

        // Assert
        hostOptions.ShutdownTimeout.Should().Be(
            TimeSpan.FromSeconds(7),
            "FR-16 bounds the orderly shutdown with SmartHal:ShutdownTimeout");

        (await harness.StopAsync()).Should().Be(ExitCodes.Success);
    }

    [Fact]
    public async Task GivenHostedServiceFailingOnStart_WhenHostStarts_ThenExitCodeIsOneAndHostFailedIsLogged()
    {
        // Arrange
        await using var harness = await ServerHostHarness.StartAsync(
            options => options.ConfigureServices = services => services.AddHostedService(_ => new FailingService()),
            TestContext.Current.CancellationToken);

        // Act
        var exitCode = await harness.Completion;

        // Assert
        exitCode.Should().Be(ExitCodes.UnhandledError, "FR-17 returns exit code 1 for an unhandled error");
        EventIdsOf(harness).Should().Contain(
            LogEvents.Lifecycle.HostFailed,
            "the unhandled error is event 1006");
    }

    private static int[] EventIdsOf(ServerHostHarness harness)
    {
        return [.. harness.Logs.GetSnapshot().Select(record => record.Id.Id)];
    }

    /// <summary>
    /// A hosted service that remembers whether the host stopped it (AC-10, AC-11).
    /// </summary>
    private sealed class StopRecordingService : IHostedService
    {
        public bool HasStopped { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            HasStopped = true;

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// A hosted service whose <c>StopAsync</c> ignores the token it is given and only ends when the
    /// test releases it, so the shutdown runs into the configured timeout (AC-12, NFR-10).
    /// </summary>
    private sealed class UnstoppableService : IHostedService
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _release.TrySetResult();

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        // The token is deliberately unobserved; that is the point of this service.
        public Task StopAsync(CancellationToken cancellationToken) => _release.Task;
    }

    /// <summary>
    /// A hosted service that reports the moment the host cancels the token of its background work
    /// (AC-13, FR-14).
    /// </summary>
    private sealed class TokenObservingService : BackgroundService
    {
        private readonly TaskCompletionSource _stoppingTokenCancelled =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task StoppingTokenCancelled => _stoppingTokenCancelled.Task;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var registration = stoppingToken.Register(() => _stoppingTokenCancelled.TrySetResult());

            await _stoppingTokenCancelled.Task.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A hosted service that fails while the host is starting it (FR-17).
    /// </summary>
    private sealed class FailingService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("This hosted service fails on purpose.");

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
