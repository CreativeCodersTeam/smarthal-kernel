using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using SmartHal.IntegrationTests.Hosting;
using SmartHal.Server.Composition;
using SmartHal.Server.Configuration;
using SmartHal.Server.Diagnostics;
using Xunit;

namespace SmartHal.IntegrationTests.Configuration;

/// <summary>
/// Drives the server host in process and verifies that the options of the <c>SmartHal</c> section are
/// bound and validated before a hosted service runs (AC-1, AC-2, AC-6, AC-7, AC-8).
/// </summary>
/// <remarks>
/// The harness sets process-wide environment variables, so the tests run in the serialised
/// <c>ProcessEnvironment</c> collection.
/// </remarks>
[Collection("ProcessEnvironment")]
public sealed class OptionsValidationTests : IDisposable
{
    private const string InstanceNameKey = "SmartHal:InstanceName";

    private const string DataDirectoryKey = "SmartHal:DataDirectory";

    private const string ShutdownTimeoutKey = "SmartHal:ShutdownTimeout";

    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"smarthal-options-{Guid.NewGuid():N}");

    public OptionsValidationTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    /// <summary>
    /// Gets a value indicating whether the unwritable data directory cannot be proven here.
    /// </summary>
    /// <value>
    /// <see langword="true"/> on Windows and for the superuser, where the plan leaves this criterion
    /// to a manual check rather than to a permission bit the test would have to set.
    /// </value>
    public static bool CannotProveUnwritablePath =>
        OperatingSystem.IsWindows()
        || string.Equals(Environment.UserName, "root", StringComparison.Ordinal);

    [Fact]
    public async Task GivenMinimalConfiguration_WhenHostStarts_ThenShutdownTimeoutIsThirtySeconds()
    {
        // Arrange & Act
        // The harness supplies exactly InstanceName and DataDirectory, which is the minimal
        // configuration of AC-1.
        await using var harness = await ServerHostHarness.StartAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        harness.Started.Should().BeTrue();

        var options = harness.Services.GetRequiredService<IOptions<SmartHalOptions>>().Value;
        options.ShutdownTimeout.Should().Be(TimeSpan.FromSeconds(30));
        options.InstanceName.Should().Be("test");
        (await harness.StopAsync()).Should().Be(ExitCodes.Success);
    }

    [Fact]
    public async Task GivenFullConfiguration_WhenHostStarts_ThenEveryFieldCarriesTheConfiguredValue()
    {
        // Arrange
        var dataDirectory = Path.Combine(_tempDirectory, "full");

        // Act
        await using var harness = await ServerHostHarness.StartAsync(
            options =>
            {
                options.Configuration[InstanceNameKey] = "living-room";
                options.Configuration[DataDirectoryKey] = dataDirectory;
                options.Configuration[ShutdownTimeoutKey] = "00:01:15";
            },
            TestContext.Current.CancellationToken);

        // Assert
        harness.Started.Should().BeTrue();

        var options = harness.Services.GetRequiredService<IOptions<SmartHalOptions>>().Value;
        options.InstanceName.Should().Be("living-room");
        options.DataDirectory.Should().Be(dataDirectory);
        options.ShutdownTimeout.Should().Be(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task GivenMissingInstanceName_WhenHostStarts_ThenStartAbortsBeforeHostedServicesWithFieldNamedAndExitCodeTwo()
    {
        // Arrange & Act
        await using var harness = await ServerHostHarness.StartAsync(
            options => options.Configuration[InstanceNameKey] = null,
            TestContext.Current.CancellationToken);

        var exitCode = await harness.Completion;

        // Assert
        harness.Started.Should().BeFalse("FR-23 aborts the start before a hosted service runs");
        exitCode.Should().Be(ExitCodes.InvalidConfiguration);
        ConfigurationInvalidRecords(harness).Should().Contain(
            record => FieldOf(record) == "InstanceName" && SectionOf(record) == "SmartHal",
            "AC-6 names the section and the field of every violation");
    }

    [Fact]
    public async Task GivenMissingInstanceNameAndNegativeShutdownTimeout_WhenHostStarts_ThenBothFieldsAreReported()
    {
        // Arrange & Act
        await using var harness = await ServerHostHarness.StartAsync(
            options =>
            {
                options.Configuration[InstanceNameKey] = null;
                options.Configuration[ShutdownTimeoutKey] = "-00:00:01";
            },
            TestContext.Current.CancellationToken);

        var exitCode = await harness.Completion;

        // Assert
        exitCode.Should().Be(ExitCodes.InvalidConfiguration);

        var reportedFields = ConfigurationInvalidRecords(harness).Select(FieldOf).ToArray();
        reportedFields.Should().Contain("InstanceName");
        reportedFields.Should().Contain(
            "ShutdownTimeout",
            "FR-25 reports every violation, not only the first one found");
    }

    [Fact]
    public async Task GivenNonExistentDataDirectoryUnderWritablePath_WhenHostStarts_ThenDirectoryIsCreatedAndHostStarts()
    {
        // Arrange
        var dataDirectory = Path.Combine(_tempDirectory, "missing", "data");
        Directory.Exists(dataDirectory).Should().BeFalse("the directory is the one the host has to create");

        // Act
        await using var harness = await ServerHostHarness.StartAsync(
            options => options.Configuration[DataDirectoryKey] = dataDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        harness.Started.Should().BeTrue();
        Directory.Exists(dataDirectory).Should().BeTrue("FR-26 creates a data directory that does not exist");
        harness.Logs.GetSnapshot().Should().Contain(
            record => record.Id.Id == LogEvents.Configuration.DataDirectoryCreated,
            "creating the data directory is event 1104");
        (await harness.StopAsync()).Should().Be(ExitCodes.Success);
    }

    [Fact(
        Skip = "A regular file as the parent path is refused by the file system, but the plan leaves this criterion to a manual check on Windows and for the superuser.",
        SkipWhen = nameof(CannotProveUnwritablePath))]
    public async Task GivenUnwritableDataDirectory_WhenHostStarts_ThenExitCodeIsTwo()
    {
        // Arrange
        // A regular file as the parent path makes the data directory impossible to create without
        // touching a permission bit, which keeps the test platform neutral (Constraint 16).
        var parentFile = Path.Combine(_tempDirectory, "not-a-directory");
        await File.WriteAllTextAsync(
            parentFile,
            "this is a file, not a directory",
            TestContext.Current.CancellationToken);

        // Act
        await using var harness = await ServerHostHarness.StartAsync(
            options => options.Configuration[DataDirectoryKey] = Path.Combine(parentFile, "data"),
            TestContext.Current.CancellationToken);

        var exitCode = await harness.Completion;

        // Assert
        harness.Started.Should().BeFalse();
        exitCode.Should().Be(ExitCodes.InvalidConfiguration);
        ConfigurationInvalidRecords(harness).Should().Contain(record => FieldOf(record) == "DataDirectory");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static IReadOnlyList<FakeLogRecord> ConfigurationInvalidRecords(ServerHostHarness harness)
    {
        return [.. harness.Logs.GetSnapshot()
            .Where(record => record.Id.Id == LogEvents.Configuration.ConfigurationInvalid)];
    }

    private static string? SectionOf(FakeLogRecord record) => ValueOf(record, "Section");

    private static string? FieldOf(FakeLogRecord record) => ValueOf(record, "Field");

    private static string? ValueOf(FakeLogRecord record, string name)
    {
        return record.StructuredState?
            .FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.Ordinal))
            .Value;
    }
}
