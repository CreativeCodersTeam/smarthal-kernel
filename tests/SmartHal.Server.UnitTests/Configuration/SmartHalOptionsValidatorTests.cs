using System.Globalization;
using System.Reflection;
using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartHal.Server.Composition;
using SmartHal.Server.Configuration;
using Xunit;

namespace SmartHal.Server.UnitTests.Configuration;

/// <summary>
/// Verifies the option table of IF-4 and the rule table of the validator against a real temporary
/// directory (IF-4, FR-21, FR-22, FR-25, FR-26).
/// </summary>
/// <remarks>
/// The file system is used as it is instead of behind an abstraction: whether a directory can be
/// created and written to is exactly the behaviour under test (plan section 3, mock boundary c).
/// The logger is the one dependency that is faked, because it only receives the event that the data
/// directory was created.
/// </remarks>
public sealed class SmartHalOptionsValidatorTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"smarthal-validator-{Guid.NewGuid():N}");

    private readonly SmartHalOptionsValidator _validator =
        new(A.Fake<ILogger<SmartHalOptionsValidator>>());

    public SmartHalOptionsValidatorTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    /// <summary>
    /// Gets the rows of the rule table of the validator (FR-22).
    /// </summary>
    /// <value>
    /// The instance name, the shutdown timeout - <see langword="null"/> leaves it at its default -
    /// and the field the violation is expected on, or <see langword="null"/> when the row is valid.
    /// </value>
    public static TheoryData<string, string?, string?> RuleTable => new()
    {
        { "", null, "InstanceName" },
        { "   ", null, "InstanceName" },
        { new string('a', 65), null, "InstanceName" },
        { new string('a', 64), null, null },
        { "instance", "00:00:00", "ShutdownTimeout" },
        { "instance", "-00:00:01", "ShutdownTimeout" },
        { "instance", "00:05:01", "ShutdownTimeout" },
        { "instance", "00:05:00", null }
    };

    [Theory]
    // One row per cell of the table of IF-4: the instance name length, the configured shutdown
    // timeout - null leaves it at its default - the data directory - null uses the temporary one -
    // and whether the row is valid.
    [InlineData(8, null, null, true)]
    [InlineData(0, null, null, false)]
    [InlineData(64, null, null, true)]
    [InlineData(65, null, null, false)]
    [InlineData(8, "00:05:00", null, true)]
    [InlineData(8, "00:05:01", null, false)]
    [InlineData(8, "00:00:00", null, false)]
    [InlineData(8, null, "", false)]
    public void Validate_OptionsTable_MatchesSpec(
        int instanceNameLength,
        string? shutdownTimeout,
        string? dataDirectory,
        bool expectValid)
    {
        // Arrange
        var options = new SmartHalOptions
        {
            InstanceName = new string('a', instanceNameLength),
            DataDirectory = dataDirectory ?? Path.Combine(_tempDirectory, "data")
        };

        if (shutdownTimeout is null)
        {
            options.ShutdownTimeout.Should().Be(
                TimeSpan.FromSeconds(30),
                "IF-4 gives ShutdownTimeout the default 00:00:30");
        }
        else
        {
            options.ShutdownTimeout = TimeSpan.Parse(shutdownTimeout, CultureInfo.InvariantCulture);
        }

        // Act
        var result = _validator.Validate(Options.DefaultName, options);

        // Assert
        result.Succeeded.Should().Be(expectValid);
    }

    [Theory]
    [MemberData(nameof(RuleTable))]
    public void Validate_RuleTable_ReportsEachViolation(
        string instanceName,
        string? shutdownTimeout,
        string? expectedField)
    {
        // Arrange
        var options = new SmartHalOptions
        {
            InstanceName = instanceName,
            DataDirectory = Path.Combine(_tempDirectory, "data")
        };

        if (shutdownTimeout is not null)
        {
            options.ShutdownTimeout = TimeSpan.Parse(shutdownTimeout, CultureInfo.InvariantCulture);
        }

        // Act
        var result = _validator.Validate(Options.DefaultName, options);

        // Assert
        if (expectedField is null)
        {
            result.Succeeded.Should().BeTrue();
        }
        else
        {
            result.Failed.Should().BeTrue();
            result.Failures.Should().ContainSingle()
                .Which.Should().StartWith($"SmartHal:{expectedField}: ");
        }
    }

    [Fact]
    public void Validate_MultipleViolations_ReportsAllNotOnlyTheFirst()
    {
        // Arrange
        var options = new SmartHalOptions
        {
            InstanceName = "",
            DataDirectory = "",
            ShutdownTimeout = TimeSpan.FromSeconds(-1)
        };

        // Act
        var result = _validator.Validate(Options.DefaultName, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().HaveCount(3, "FR-25 collects every violation, not only the first");
        result.Failures.Should().Contain(failure => failure.StartsWith("SmartHal:InstanceName: ", StringComparison.Ordinal));
        result.Failures.Should().Contain(failure => failure.StartsWith("SmartHal:DataDirectory: ", StringComparison.Ordinal));
        result.Failures.Should().Contain(failure => failure.StartsWith("SmartHal:ShutdownTimeout: ", StringComparison.Ordinal));
    }

    [Theory]
    // true: the directory is missing below a writable parent and has to be created.
    // false: the parent path is a regular file, so no directory can ever be created below it. That
    // makes the path unwritable on every platform without a permission bit (Constraint 16).
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_DataDirectory_CreatesMissingDirectoryAndRejectsUnwritable(bool parentIsWritable)
    {
        // Arrange
        string dataDirectory;

        if (parentIsWritable)
        {
            dataDirectory = Path.Combine(_tempDirectory, "missing", "data");
        }
        else
        {
            var parentFile = Path.Combine(_tempDirectory, "not-a-directory");
            File.WriteAllText(parentFile, "this is a file, not a directory");
            dataDirectory = Path.Combine(parentFile, "data");
        }

        var options = new SmartHalOptions { InstanceName = "instance", DataDirectory = dataDirectory };

        // Act
        var result = _validator.Validate(Options.DefaultName, options);

        // Assert
        if (parentIsWritable)
        {
            result.Succeeded.Should().BeTrue();
            Directory.Exists(dataDirectory).Should().BeTrue("FR-26 creates a data directory that does not exist");
            Directory.GetFiles(dataDirectory).Should().BeEmpty("the write probe removes its file again");
        }
        else
        {
            result.Failed.Should().BeTrue();
            result.Failures.Should().ContainSingle()
                .Which.Should().StartWith("SmartHal:DataDirectory: ");
        }
    }

    [Fact]
    public void SmartHalOptions_Type_HasExactlyTheThreeSpecifiedProperties()
    {
        // Arrange
        var optionsType = typeof(SmartHalOptions);

        // Act
        var properties = optionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Assert
        properties.Select(property => property.Name).Should().BeEquivalentTo(
            ["InstanceName", "DataDirectory", "ShutdownTimeout"],
            "FR-21 gives the section exactly the three fields of section 6.4");
        optionsType.GetProperty("InstanceName")!.PropertyType.Should().Be<string>();
        optionsType.GetProperty("DataDirectory")!.PropertyType.Should().Be<string>();
        optionsType.GetProperty("ShutdownTimeout")!.PropertyType.Should().Be<TimeSpan>();
        SmartHalOptions.SectionName.Should().Be("SmartHal");
    }

    [Fact]
    public void ServiceRegistration_AddSmartHalOptions_RegistersValidateOnStart()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();

        // Act
        ServiceRegistration.AddSmartHalOptions(services, configuration);

        // Assert
        services.Should().Contain(
            descriptor => descriptor.ServiceType == typeof(IStartupValidator),
            "ValidateOnStart registers the validator the host runs before its hosted services (FR-22)");
        services.Should().Contain(
            descriptor => descriptor.ServiceType == typeof(IValidateOptions<SmartHalOptions>)
                && descriptor.ImplementationType == typeof(SmartHalOptionsValidator));

        using var provider = services.BuildServiceProvider();
        var validate = () => provider.GetRequiredService<IStartupValidator>().Validate();
        validate.Should().Throw<OptionsValidationException>(
            "an empty configuration leaves the mandatory fields unset, and validation happens at start");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
