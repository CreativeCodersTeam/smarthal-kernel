using System.Runtime.InteropServices;
using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Testing;
using SmartHal.Server.Composition;
using SmartHal.Server.Diagnostics;
using SmartHal.Server.Hosting;
using Xunit;

namespace SmartHal.Server.UnitTests.Hosting;

/// <summary>
/// Verifies the rule of the shutdown signals: the first signal asks the host to stop, a second
/// interrupt aborts, and a repeated termination signal is ignored (FR-15, G-7).
/// </summary>
/// <remarks>
/// The signals are handled in process rather than sent to the operating system, so the test runs on
/// every platform - Windows knows no <c>SIGTERM</c> (C-4, Constraint 16). The lifetime is a
/// FakeItEasy fake, because the assertion is about the call the handler makes on it; the logger is
/// the <c>FakeLogger</c> of <c>Microsoft.Extensions.Diagnostics.Testing</c>, because the assertion is
/// about the <c>EventId</c> of the record rather than about a message.
/// </remarks>
public sealed class ShutdownSignalHandlerTests
{
    [Theory]
    [InlineData(PosixSignal.SIGTERM, null, false)]
    [InlineData(PosixSignal.SIGINT, null, false)]
    [InlineData(PosixSignal.SIGINT, PosixSignal.SIGINT, true)]
    [InlineData(PosixSignal.SIGTERM, PosixSignal.SIGTERM, false)]
    [InlineData(PosixSignal.SIGTERM, PosixSignal.SIGINT, true)]
    public void Handle_FirstSignalThenSecondSigint_StopsApplicationThenTerminates(
        PosixSignal firstSignal,
        PosixSignal? secondSignal,
        bool expectsTermination)
    {
        // Arrange
        var lifetime = A.Fake<IHostApplicationLifetime>();
        var logger = new FakeLogger<ShutdownSignalHandler>();
        var terminations = new List<int>();
        var handler = new ShutdownSignalHandler(lifetime, logger, terminations.Add);

        // Act
        handler.Handle(firstSignal);

        if (secondSignal is { } repeatedSignal)
        {
            handler.Handle(repeatedSignal);
        }

        // Assert
        A.CallTo(() => lifetime.StopApplication()).MustHaveHappenedOnceExactly();

        if (expectsTermination)
        {
            EventIdsOf(logger).Should().Equal(
                [LogEvents.Lifecycle.ShutdownRequested, LogEvents.Lifecycle.ShutdownAborted],
                "a second interrupt aborts the orderly shutdown (FR-15)");
            terminations.Should().Equal(
                [ExitCodes.UnhandledError],
                "the plan ends the aborted shutdown with exit code 1 (G-7)");
        }
        else
        {
            EventIdsOf(logger).Should().Equal(
                [LogEvents.Lifecycle.ShutdownRequested],
                "only the first signal starts the orderly shutdown");
            terminations.Should().BeEmpty("nothing but a second interrupt ends the process at once");
        }
    }

    [Fact]
    public void Handle_FirstSignal_LogsTheSignalName()
    {
        // Arrange
        var logger = new FakeLogger<ShutdownSignalHandler>();
        var handler = new ShutdownSignalHandler(A.Fake<IHostApplicationLifetime>(), logger, _ => { });

        // Act
        handler.Handle(PosixSignal.SIGTERM);

        // Assert
        // IF-6 carries the signal name in event 1002, so the log tells the two signals apart.
        logger.Collector.GetSnapshot().Should().ContainSingle()
            .Which.Message.Should().Contain("SIGTERM", "event 1002 names the signal that arrived");
    }

    private static int[] EventIdsOf(FakeLogger logger)
    {
        return [.. logger.Collector.GetSnapshot().Select(record => record.Id.Id)];
    }
}
