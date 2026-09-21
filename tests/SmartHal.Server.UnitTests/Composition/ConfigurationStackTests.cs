using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Hosting;
using SmartHal.Server.Composition;
using Xunit;

namespace SmartHal.Server.UnitTests.Composition;

/// <summary>
/// Verifies the configuration stack of section 6.3 against a real <see cref="HostApplicationBuilder"/>
/// (IF-3, FR-17, FR-18, FR-19, FR-20).
/// </summary>
/// <remarks>
/// The tests read and write process-wide environment variables, so they run in the serialised
/// <c>ProcessEnvironment</c> collection and restore every variable they touch.
/// </remarks>
[Collection("ProcessEnvironment")]
public sealed class ConfigurationStackTests : IDisposable
{
    private const string ConfigFileVariable = "SMARTHAL_CONFIG_FILE";

    private const string PrefixedInstanceNameVariable = "SMARTHAL_SmartHal__InstanceName";

    private const string UnprefixedProbeVariable = "SmartHal__UnprefixedProbe";

    private const string InstanceNameKey = "SmartHal:InstanceName";

    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"smarthal-configuration-{Guid.NewGuid():N}");

    private readonly Dictionary<string, string?> _originalVariables = new(StringComparer.Ordinal);

    /// <summary>
    /// The three ways the external configuration file of <c>SMARTHAL_CONFIG_FILE</c> can present itself.
    /// </summary>
    public enum ExternalConfigFileCase
    {
        /// <summary>The variable is not set at all.</summary>
        Unset,

        /// <summary>The variable is set but points at a file that does not exist.</summary>
        Missing,

        /// <summary>The variable points at a file whose content is not valid JSON.</summary>
        Invalid
    }

    public ConfigurationStackTests()
    {
        Directory.CreateDirectory(_tempDirectory);
        SetVariable(ConfigFileVariable, null);
        SetVariable(PrefixedInstanceNameVariable, null);
        SetVariable(UnprefixedProbeVariable, null);
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    public void Apply_DefaultBuilder_ProducesExactlyTheSixSourcesInOrder(
        string environmentName,
        bool expectsUserSecrets)
    {
        // Arrange
        var externalFile = WriteFile("external.json", """{ "SmartHal": { "InstanceName": "from-file" } }""");
        SetVariable(ConfigFileVariable, externalFile);
        var builder = Host.CreateApplicationBuilder(["--environment", environmentName]);

        // Act
        ConfigurationStack.Apply(builder);

        // Assert
        var sources = builder.Configuration.Sources;
        var jsonPaths = sources.OfType<JsonConfigurationSource>().Select(source => source.Path).ToArray();
        var expectedJsonPaths = expectsUserSecrets
            ? new[] { "appsettings.json", $"appsettings.{environmentName}.json", "external.json", "secrets.json" }
            : ["appsettings.json", $"appsettings.{environmentName}.json", "external.json"];

        sources.Should().HaveCount(expectsUserSecrets ? 6 : 5);
        jsonPaths.Should().Equal(expectedJsonPaths);
        sources.Take(jsonPaths.Length).Should().AllBeOfType<JsonConfigurationSource>();
        sources[^2].Should().BeOfType<EnvironmentVariablesConfigurationSource>()
            .Which.Prefix.Should().Be("SMARTHAL_");
        sources[^1].Should().BeOfType<CommandLineConfigurationSource>();
    }

    [Theory]
    [InlineData(null, "from-environment")]
    [InlineData("--SmartHal:InstanceName=from-command-line", "from-command-line")]
    public void Apply_SameKeyInMultipleSources_LaterSourceWins(string? commandLineArgument, string expectedValue)
    {
        // Arrange
        var externalFile = WriteFile("layered.json", """{ "SmartHal": { "InstanceName": "from-file" } }""");
        SetVariable(ConfigFileVariable, externalFile);
        SetVariable(PrefixedInstanceNameVariable, "from-environment");
        string[] args = commandLineArgument is null ? [] : [commandLineArgument];
        var builder = Host.CreateApplicationBuilder(args);

        // Act
        ConfigurationStack.Apply(builder);

        // Assert
        builder.Configuration[InstanceNameKey].Should().Be(expectedValue);
    }

    [Fact]
    public void Apply_EnvironmentVariableWithPrefixAndDoubleUnderscore_MapsToSectionPath()
    {
        // Arrange
        SetVariable(PrefixedInstanceNameVariable, "from-environment");
        SetVariable(UnprefixedProbeVariable, "from-unprefixed-environment");
        var builder = Host.CreateApplicationBuilder([]);

        // Act
        ConfigurationStack.Apply(builder);

        // Assert
        builder.Configuration[InstanceNameKey].Should().Be("from-environment");
        builder.Configuration["SmartHal:UnprefixedProbe"].Should()
            .BeNull("G-11 loads environment variables only with the SMARTHAL_ prefix");
    }

    [Theory]
    [InlineData(ExternalConfigFileCase.Unset)]
    [InlineData(ExternalConfigFileCase.Missing)]
    [InlineData(ExternalConfigFileCase.Invalid)]
    public void Apply_ExternalConfigFileVariable_SkippedWhenUnsetOrMissingAndFailsWhenInvalid(
        ExternalConfigFileCase fileCase)
    {
        // Arrange
        SetVariable(ConfigFileVariable, fileCase switch
        {
            ExternalConfigFileCase.Unset => null,
            ExternalConfigFileCase.Missing => Path.Combine(_tempDirectory, "does-not-exist.json"),
            _ => WriteFile("invalid.json", "{ this is not valid json")
        });
        var builder = Host.CreateApplicationBuilder([]);
        var apply = () => ConfigurationStack.Apply(builder);

        // Act & Assert
        if (fileCase == ExternalConfigFileCase.Invalid)
        {
            apply.Should().Throw<InvalidDataException>("FR-20 aborts the start when the file is unreadable");
        }
        else
        {
            apply.Should().NotThrow("FR-20 makes the external configuration file optional");
            builder.Configuration[InstanceNameKey].Should().BeNull();
        }
    }

    [Fact]
    public async Task RunAsync_ConfigurationBuildFails_ReturnsInvalidConfiguration()
    {
        // Arrange
        SetVariable(ConfigFileVariable, _tempDirectory);

        // Act
        var exitCode = await ServerHost.RunAsync([], cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        exitCode.Should().Be(ExitCodes.InvalidConfiguration);
    }

    public void Dispose()
    {
        foreach (var variable in _originalVariables)
        {
            Environment.SetEnvironmentVariable(variable.Key, variable.Value);
        }

        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private string WriteFile(string fileName, string content)
    {
        var path = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(path, content);

        return path;
    }

    private void SetVariable(string name, string? value)
    {
        if (!_originalVariables.ContainsKey(name))
        {
            _originalVariables[name] = Environment.GetEnvironmentVariable(name);
        }

        Environment.SetEnvironmentVariable(name, value);
    }
}
