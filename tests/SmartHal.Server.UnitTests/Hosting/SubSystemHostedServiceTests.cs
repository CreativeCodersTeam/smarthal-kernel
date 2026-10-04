using AwesomeAssertions;
using Microsoft.Extensions.Logging.Testing;
using SmartHal.Server.Diagnostics;
using SmartHal.Server.Hosting;
using Xunit;

namespace SmartHal.Server.UnitTests.Hosting;

/// <summary>
/// Verifies that <see cref="SubSystemHostedService"/> writes one event per lifecycle step.
/// </summary>
public sealed class SubSystemHostedServiceTests
{
    [Fact]
    public async Task LifecycleMethods_AllCalled_WriteOneEventPerStep()
    {
        // Arrange
        var logger = new FakeLogger<SubSystemHostedService>();
        var service = new SubSystemHostedService(logger);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await service.StartingAsync(cancellationToken);
        await service.StartAsync(cancellationToken);
        await service.StartedAsync(cancellationToken);
        await service.StoppingAsync(cancellationToken);
        await service.StopAsync(cancellationToken);
        await service.StoppedAsync(cancellationToken);

        // Assert
        var records = logger.Collector.GetSnapshot();
        records.Should().OnlyContain(record => record.Id.Id == LogEvents.SubSystem.SubSystemLifecycleStep);
        records.Select(record => record.Message).Should().Equal(
            "The sub-system passed the lifecycle step 'StartingAsync'.",
            "The sub-system passed the lifecycle step 'StartAsync'.",
            "The sub-system passed the lifecycle step 'StartedAsync'.",
            "The sub-system passed the lifecycle step 'StoppingAsync'.",
            "The sub-system passed the lifecycle step 'StopAsync'.",
            "The sub-system passed the lifecycle step 'StoppedAsync'.");
    }
}
