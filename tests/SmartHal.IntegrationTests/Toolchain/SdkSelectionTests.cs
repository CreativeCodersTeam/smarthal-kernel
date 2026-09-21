using System.Diagnostics;
using AwesomeAssertions;
using SmartHal.IntegrationTests.Hosting;
using Xunit;

namespace SmartHal.IntegrationTests.Toolchain;

/// <summary>
/// Verifies that <c>global.json</c> in the repository root pins the SDK 10 band (AC-31).
/// </summary>
public sealed class SdkSelectionTests
{
    [Fact]
    public void GivenRepositoryRoot_WhenDotnetVersionIsQueried_ThenSdk10IsUsed()
    {
        // Arrange
        var startInfo = new ProcessStartInfo("dotnet", "--version")
        {
            WorkingDirectory = RepositoryLocator.FindRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        // Act
        using var process = Process.Start(startInfo);
        process.Should().NotBeNull();

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        var exited = process.WaitForExit(TimeSpan.FromMinutes(2));

        // Assert
        exited.Should().BeTrue("'dotnet --version' must answer within two minutes");
        process.ExitCode.Should().Be(0, standardError);
        standardOutput.Trim().Should().StartWith("10.");
    }
}
