using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartHal.IntegrationTests.Hosting;
using SmartHal.Server.Composition;
using Xunit;

namespace SmartHal.IntegrationTests.Configuration;

/// <summary>
/// Drives the server host in process and verifies the layered configuration, the host services and
/// the exit codes of the start sequence (AC-3, AC-4, AC-5, IF-5, FR-13).
/// </summary>
/// <remarks>
/// The harness sets process-wide environment variables, so the tests run in the serialised
/// <c>ProcessEnvironment</c> collection.
/// </remarks>
[Collection("ProcessEnvironment")]
public sealed class ConfigurationLayeringTests : IDisposable
{
    private const string ConfigFileVariable = "SMARTHAL_CONFIG_FILE";

    private const string PrefixedInstanceNameVariable = "SMARTHAL_SmartHal__InstanceName";

    private const string InstanceNameKey = "SmartHal:InstanceName";

    private const string MinimumLevelKey = "Serilog:MinimumLevel:Default";

    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"smarthal-layering-{Guid.NewGuid():N}");

    public ConfigurationLayeringTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task GivenInstanceNameInAppSettingsAndEnvironmentVariable_WhenHostStarts_ThenEnvironmentVariableWins()
    {
        // Arrange
        // The shipped appsettings.json carries an empty SmartHal section, so the lower layer of this
        // criterion is the file of SMARTHAL_CONFIG_FILE (source 3) and the upper one the prefixed
        // environment variable (source 5).
        var externalFile = WriteFile(
            "instance-name.json",
            $$"""{ "SmartHal": { "InstanceName": "from-file", "DataDirectory": {{DataDirectoryLiteral()}} } }""");

        // Act
        await using var harness = await ServerHostHarness.StartAsync(
            options =>
            {
                options.UseTemporaryDataDirectory = false;
                options.EnvironmentVariables[ConfigFileVariable] = externalFile;
                options.EnvironmentVariables[PrefixedInstanceNameVariable] = "from-environment";
            },
            TestContext.Current.CancellationToken);

        // Assert
        harness.Started.Should().BeTrue();
        harness.Services.GetRequiredService<IConfiguration>()[InstanceNameKey].Should().Be("from-environment");
    }

    [Fact]
    public async Task GivenExternalConfigFile_WhenHostStarts_ThenItsValuesOverrideAppSettings()
    {
        // Arrange
        // MinimumLevelKey is a key the shipped appsettings.json really carries, so the external file
        // overrides source 1 instead of merely filling a gap in it (D-11, AC-4).
        var shippedMinimumLevel = ShippedAppSettings()[MinimumLevelKey];
        shippedMinimumLevel.Should().Be("Information", "the shipped appsettings.json sets this key");

        // DataDirectory is mandatory from task #5 on, so the file carries it as well; without it the
        // host would abort before it could prove that the file wins over appsettings.json.
        var externalFile = WriteFile(
            "override.json",
            $$"""
            {
              "SmartHal": { "InstanceName": "from-external-file", "DataDirectory": {{DataDirectoryLiteral()}} },
              "Serilog": { "MinimumLevel": { "Default": "Warning" } }
            }
            """);

        // Act
        await using var harness = await ServerHostHarness.StartAsync(
            options =>
            {
                options.UseTemporaryDataDirectory = false;
                options.EnvironmentVariables[ConfigFileVariable] = externalFile;
            },
            TestContext.Current.CancellationToken);

        // Assert
        harness.Started.Should().BeTrue();

        var configuration = harness.Services.GetRequiredService<IConfiguration>();
        configuration[InstanceNameKey].Should().Be("from-external-file");
        configuration[MinimumLevelKey].Should().Be("Warning");
        configuration[MinimumLevelKey].Should().NotBe(
            shippedMinimumLevel,
            "the external file wins over the value appsettings.json ships");
    }

    [Fact]
    public async Task GivenNoExternalConfigFileVariable_WhenHostStarts_ThenHostStartsUnchanged()
    {
        // Arrange & Act
        await using var harness = await ServerHostHarness.StartAsync(
            options => options.EnvironmentVariables[ConfigFileVariable] = null,
            TestContext.Current.CancellationToken);

        // Assert
        harness.Started.Should().BeTrue("FR-20 makes the external configuration file optional");
        harness.Services.GetRequiredService<IConfiguration>()[InstanceNameKey].Should().Be("test");
        (await harness.StopAsync()).Should().Be(ExitCodes.Success);
    }

    [Fact]
    public async Task GivenExternalConfigFileWithInvalidJson_WhenHostStarts_ThenExitCodeIsTwo()
    {
        // Arrange
        var externalFile = WriteFile("broken.json", "{ \"SmartHal\": { this is not valid json ");

        // Act
        await using var harness = await ServerHostHarness.StartAsync(
            options => options.EnvironmentVariables[ConfigFileVariable] = externalFile,
            TestContext.Current.CancellationToken);

        // Assert
        harness.Started.Should().BeFalse();
        (await harness.Completion).Should().Be(ExitCodes.InvalidConfiguration);
    }

    [Fact]
    public async Task GivenStartedHost_WhenStopped_ThenExitCodeIsZero()
    {
        // Arrange
        await using var harness = await ServerHostHarness.StartAsync(cancellationToken: TestContext.Current.CancellationToken);
        harness.Started.Should().BeTrue();

        // Act
        var exitCode = await harness.StopAsync();

        // Assert
        exitCode.Should().Be(ExitCodes.Success);
        harness.Stopped.Should().BeTrue();
    }

    [Fact]
    public async Task GivenStartedHost_WhenServicesResolved_ThenHostServicesAreAvailable()
    {
        // Arrange
        await using var harness = await ServerHostHarness.StartAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var lifetime = harness.Services.GetRequiredService<IHostApplicationLifetime>();
        var environment = harness.Services.GetRequiredService<IHostEnvironment>();

        // Assert
        lifetime.Should().NotBeNull();
        environment.EnvironmentName.Should().Be("Production");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static IConfigurationRoot ShippedAppSettings()
    {
        return new ConfigurationBuilder()
            .AddJsonFile(
                Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
                optional: false,
                reloadOnChange: false)
            .Build();
    }

    private string DataDirectoryLiteral()
    {
        return System.Text.Json.JsonSerializer.Serialize(Path.Combine(_tempDirectory, "data"));
    }

    private string WriteFile(string fileName, string content)
    {
        var path = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(path, content);

        return path;
    }
}
