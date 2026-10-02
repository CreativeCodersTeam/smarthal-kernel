using System.Globalization;
using AwesomeAssertions;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Integration;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;
using SmartHal.Core.Abstractions.Validation;
using SmartHal.Core.Validation;
using Xunit;
using static SmartHal.Core.UnitTests.Validation.ValidationFixtures;

namespace SmartHal.Core.UnitTests.Validation;

/// <summary>
/// Verifies the checks of values that only make sense in one direction: length limits, patterns, timeouts,
/// retentions, intervals and deadbands, and that messages format numbers with the invariant culture.
/// </summary>
public sealed class ValueRulesTests
{
    private readonly ContractValidator _sut = new ContractValidator();

    public static TheoryData<DataType, string, string> InvalidDataTypes =>
        new TheoryData<DataType, string, string>
    {
        { new StringType(MaxLength: -1), ValidationCodes.InvalidRange, "dataType.maxLength" },
        { new ArrayType(new BooleanType(), MaxItems: -1), ValidationCodes.InvalidRange, "dataType.maxItems" },
        { new StringType(Pattern: "(["), ValidationCodes.InvalidPattern, "dataType.pattern" }
    };

    public static TheoryData<DataType> ValidDataTypes =>
    [
        new StringType(MaxLength: 0),
        new ArrayType(new BooleanType(), MaxItems: 0),
        new StringType(64, "^[a-z0-9._-]+$")
    ];

    [Theory]
    [MemberData(nameof(InvalidDataTypes))]
    public void Validate_DataTypeWithInvalidLimitOrPattern_ReportsTheViolation(DataType dataType, string code, string path)
    {
        // Arrange
        var definition = new DataTypeDef("vendor.text", new TypeVersion(1, 0), dataType);

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.CodesAndPaths().Should().Equal((code, path));
    }

    [Theory]
    [MemberData(nameof(ValidDataTypes))]
    public void Validate_DataTypeWithValidLimitOrPattern_ReportsNoViolation(DataType dataType)
    {
        // Arrange
        var definition = new DataTypeDef("vendor.text", new TypeVersion(1, 0), dataType);

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_CommandTimeoutNotPositive_ReportsInvalidDuration(int seconds)
    {
        // Arrange
        var type = WithCommandTimeout(TimeSpan.FromSeconds(seconds));

        // Act
        var errors = _sut.Validate(type);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.InvalidDuration, "commands.setLevel.timeout"));
    }

    [Fact]
    public void Validate_CommandTimeoutPositive_ReportsNoViolation()
    {
        // Arrange
        var type = WithCommandTimeout(TimeSpan.FromMilliseconds(1));

        // Act
        var errors = _sut.Validate(type);

        // Assert
        errors.Should().BeEmpty();
    }

    public static TheoryData<HistoryPolicy, string, string> InvalidPolicies =>
        new TheoryData<HistoryPolicy, string, string>
    {
        { new HistoryPolicy(TimeSpan.Zero), ValidationCodes.InvalidDuration, "rawRetention" },
        { Policy(new Rollup(TimeSpan.Zero, [RollupAggregate.Avg], TimeSpan.FromDays(1))), ValidationCodes.InvalidDuration,
            "rollups[0].interval" },
        { Policy(new Rollup(TimeSpan.FromMinutes(1), [RollupAggregate.Avg], TimeSpan.FromDays(-1))), ValidationCodes.InvalidDuration,
            "rollups[0].retention" },
        { Policy(deadband: new Deadband(MinInterval: TimeSpan.FromSeconds(-1))), ValidationCodes.InvalidDuration, "deadband.minInterval" },
        { Policy(deadband: new Deadband(Absolute: -0.1)), ValidationCodes.InvalidRange, "deadband.absolute" },
        { Policy(deadband: new Deadband(Relative: double.NaN)), ValidationCodes.InvalidRange, "deadband.relative" }
    };

    [Theory]
    [MemberData(nameof(InvalidPolicies))]
    public void Validate_PropertyHistoryWithInvalidValue_ReportsTheViolation(HistoryPolicy policy, string code, string member)
    {
        // Arrange
        var type = WithHistory(policy);

        // Act
        var errors = _sut.Validate(type);

        // Assert
        errors.CodesAndPaths().Should().Equal((code, $"properties.level.history.{member}"));
    }

    [Fact]
    public void Validate_PropertyHistoryWithBoundaryValues_ReportsNoViolation()
    {
        // Arrange
        // Zero deadbands and a zero minimum interval are allowed: they store every change.
        var policy = Policy(
            new Rollup(TimeSpan.FromMinutes(1), [RollupAggregate.Min, RollupAggregate.Max], TimeSpan.FromDays(90)),
            new Deadband(0, 0, TimeSpan.Zero));
        var type = WithHistory(policy);

        // Act
        var errors = _sut.Validate(type);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_HistoryOverrideWithZeroRetention_ReportsInvalidDuration()
    {
        // Arrange
        var capability = LevelCapability() with
        {
            HistoryOverrides = new Dictionary<string, HistoryPolicy> { ["level"] = new HistoryPolicy(TimeSpan.Zero) }
        };

        // Act
        var errors = _sut.Validate(DimmerDevice(capability));

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.InvalidDuration, "channels[1].capabilities[0].historyOverrides.level.rawRetention"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_SleepyWakeIntervalNotPositive_ReportsInvalidDuration(int seconds)
    {
        // Arrange
        var deviceType = Dimmer() with { Sleepy = new SleepyConfig(TimeSpan.FromSeconds(seconds)) };

        // Act
        var errors = _sut.Validate(deviceType);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.InvalidDuration, "sleepy.wakeInterval"));
    }

    [Fact]
    public void Validate_SleepyWakeIntervalPositive_ReportsNoViolation()
    {
        // Arrange
        var deviceType = Dimmer() with { Sleepy = new SleepyConfig(TimeSpan.FromMinutes(5)) };

        // Act
        var errors = _sut.Validate(deviceType);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidPattern_ReportsAStableMessageWithoutExceptionText()
    {
        // Arrange
        var definition = new DataTypeDef("vendor.code", new TypeVersion(1, 0), new StringType(Pattern: "("));

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.Should().ContainSingle().Which.Should().Be(
            new ValidationError("dataType.pattern", ValidationCodes.InvalidPattern, "The pattern '(' is not a valid regular expression."));
    }

    [Theory]
    [InlineData("^[a-z]+$")]
    [InlineData("^\\d{3}-\\d{4}$")]
    [InlineData("^(?:ab|cd)*$")]
    public void Validate_EcmaScriptPattern_ReportsNoViolation(string pattern)
    {
        // Arrange
        var definition = new DataTypeDef("vendor.code", new TypeVersion(1, 0), new StringType(Pattern: pattern));

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MappingPollIntervalZero_ReportsInvalidDuration()
    {
        // Arrange
        var deviceType = WithPollInterval(TimeSpan.Zero);

        // Act
        var errors = _sut.Validate(deviceType);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.InvalidDuration, "bindingTemplates[0].mappings[0].pollInterval"));
    }

    [Fact]
    public void Validate_MappingPollIntervalPositive_ReportsNoViolation()
    {
        // Arrange
        var deviceType = WithPollInterval(TimeSpan.FromSeconds(1));

        // Act
        var errors = _sut.Validate(deviceType);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_UnderGermanCulture_FormatsNumbersInMessagesInvariantly()
    {
        // Arrange
        var definition = new DataTypeDef("vendor.range", new TypeVersion(1, 0), new NumberType(Minimum: 1.5, Maximum: 0.5));
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        IReadOnlyList<ValidationError> errors;

        try
        {
            // Act
            errors = _sut.Validate(definition);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        // Assert
        errors.Should().ContainSingle().Which.Message.Should().Contain("1.5").And.Contain("0.5").And.NotContain("1,5");
    }

    private static HistoryPolicy Policy(Rollup? rollup = null, Deadband? deadband = null) =>
        new HistoryPolicy(TimeSpan.FromDays(30), rollup is null ? null : [rollup], deadband);

    private static CapabilityType WithCommandTimeout(TimeSpan timeout)
    {
        var level = Level();
        var commands = new Dictionary<string,
            CommandDef>(level.Commands) { ["setLevel"] = level.Commands["setLevel"] with { Timeout = timeout } };

        return level with { Commands = commands };
    }

    private static CapabilityType WithHistory(HistoryPolicy policy)
    {
        var level = Level();
        var properties = new Dictionary<string,
            PropertyDef>(level.Properties) { ["level"] = level.Properties["level"] with { History = policy } };

        return level with { Properties = properties };
    }

    private static DeviceType WithPollInterval(TimeSpan pollInterval)
    {
        var dimmer = Dimmer();
        var template = dimmer.BindingTemplates![0];
        var mapping = template.Mappings[0] with { PollInterval = pollInterval };

        return dimmer with { BindingTemplates = [template with { Mappings = [mapping] }] };
    }
}
