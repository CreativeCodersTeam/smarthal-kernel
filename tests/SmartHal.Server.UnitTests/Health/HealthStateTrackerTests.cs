using AwesomeAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SmartHal.Server.Health;
using Xunit;

namespace SmartHal.Server.UnitTests.Health;

/// <summary>
/// Verifies the three readiness states and the transitions between them (FR-40).
/// </summary>
/// <remarks>
/// The tracker is the pure rule behind the readiness signal, so it is exercised without a host, a
/// logger or a health check: a sequence of reported <see cref="HealthStatus"/> values goes in, the
/// state changes it produces come out.
/// </remarks>
public sealed class HealthStateTrackerTests
{
    [Theory]
    // Each row is a sequence of reported statuses and the state change each of them produces;
    // "-" means the state did not change, so Apply returns null.
    [InlineData("Healthy", "Ready")]
    [InlineData("Unhealthy", "-")]
    [InlineData("Degraded", "-")]
    [InlineData("Unhealthy,Unhealthy", "-,-")]
    [InlineData("Unhealthy,Healthy", "-,Ready")]
    [InlineData("Healthy,Healthy", "Ready,-")]
    [InlineData("Healthy,Unhealthy", "Ready,NotReady")]
    [InlineData("Healthy,Degraded", "Ready,NotReady")]
    [InlineData("Healthy,Unhealthy,Unhealthy", "Ready,NotReady,-")]
    [InlineData("Healthy,Unhealthy,Healthy", "Ready,NotReady,Ready")]
    [InlineData("Healthy,Degraded,Healthy,Unhealthy", "Ready,NotReady,Ready,NotReady")]
    public void Apply_StatusSequence_YieldsStartingReadyNotReadyTransitions(
        string reportedStatuses,
        string expectedChanges)
    {
        // Arrange
        var statuses = reportedStatuses.Split(',').Select(Enum.Parse<HealthStatus>).ToArray();
        var expected = expectedChanges.Split(',');
        var tracker = new HealthStateTracker();

        tracker.Current.Should().Be(HealthState.Starting, "no readiness check has reported yet");

        for (var step = 0; step < statuses.Length; step++)
        {
            // Act
            var change = tracker.Apply(statuses[step]);

            // Assert
            var expectedChange = expected[step] == "-"
                ? (HealthState?)null
                : Enum.Parse<HealthState>(expected[step]);

            change.Should().Be(
                expectedChange,
                $"step {step} reported {statuses[step]}");

            if (change is not null)
            {
                tracker.Current.Should().Be(change.Value);
            }
        }
    }
}
