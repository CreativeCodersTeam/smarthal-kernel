using AwesomeAssertions;
using SmartHal.Server.Diagnostics;
using Xunit;

namespace SmartHal.Server.UnitTests.Diagnostics;

/// <summary>
/// Verifies that the telemetry primitives of the server carry the name of their own assembly
/// (FR-37).
/// </summary>
public sealed class ServerTelemetryTests
{
    [Fact]
    public void ServerTelemetry_ActivitySourceAndMeter_AreNamedAfterTheAssembly()
    {
        // Arrange
        var assemblyName = typeof(ServerTelemetry).Assembly.GetName().Name;

        // Act
        var activitySourceName = ServerTelemetry.ActivitySource.Name;
        var meterName = ServerTelemetry.Meter.Name;

        // Assert
        assemblyName.Should().Be("SmartHal.Server");
        activitySourceName.Should().Be(assemblyName);
        meterName.Should().Be(assemblyName);
    }
}
