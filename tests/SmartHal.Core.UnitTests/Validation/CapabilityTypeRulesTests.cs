using AwesomeAssertions;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Schema;
using SmartHal.Core.Abstractions.Validation;
using SmartHal.Core.Validation;
using Xunit;
using static SmartHal.Core.UnitTests.Validation.ValidationFixtures;

namespace SmartHal.Core.UnitTests.Validation;

/// <summary>
/// Verifies the rules of a capability type on its own: command completion (R3), command references (R4), alarm
/// sources (R5), feature flags (R6) and null entries.
/// </summary>
public sealed class CapabilityTypeRulesTests
{
    private readonly ContractValidator _sut = new();

    [Fact]
    public void Validate_ResultCommandWithoutResultType_ReportsMissingResult()
    {
        // Arrange
        var type = WithCommand("calibrate", new CommandDef(Completion.Result, TimeSpan.FromSeconds(10)));

        // Act
        var errors = _sut.Validate(type);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.MissingResult, "commands.calibrate.result"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Validate_ConfirmedCommandWithoutAffects_ReportsMissingAffects(bool emptyList)
    {
        // Arrange
        var type = WithCommand(
            "setLevel",
            new CommandDef(Completion.Confirmed, TimeSpan.FromSeconds(30), Affects: emptyList ? [] : null));

        // Act
        var errors = _sut.Validate(type);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.MissingAffects, "commands.setLevel.affects"));
    }

    [Fact]
    public void Validate_AckCommandWithoutAffectsOrResult_ReportsNothing()
    {
        // Arrange
        var type = WithCommand("identify", new CommandDef(Completion.Ack, TimeSpan.FromSeconds(10)));

        // Act
        var errors = _sut.Validate(type);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_AffectsUnknownProperty_ReportsUnknownProperty()
    {
        // Arrange
        var type = WithCommand(
            "setLevel",
            new CommandDef(Completion.Confirmed, TimeSpan.FromSeconds(30), Affects: ["level", "brightness"]));

        // Act
        var errors = _sut.Validate(type);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.UnknownProperty, "commands.setLevel.affects[1]"));
        errors[0].Message.Should().Contain("brightness");
    }

    [Fact]
    public void Validate_RequiredParameterThatIsNotDefined_ReportsUnknownParameter()
    {
        // Arrange
        var type = WithCommand(
            "setLevel",
            new CommandDef(
                Completion.Confirmed,
                TimeSpan.FromSeconds(30),
                new Dictionary<string, DataType> { ["level"] = new NumberType() },
                Affects: ["level"],
                RequiredParameters: ["transition"]));

        // Act
        var errors = _sut.Validate(type);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.UnknownParameter, "commands.setLevel.requiredParameters[0]"));
    }

    [Fact]
    public void Validate_DeviceAlarmWithUnknownEvent_ReportsUnknownEvent()
    {
        // Arrange
        var type = WithAlarm("blockedAlarm", new AlarmDef(Severity(), "Blocked", new DeviceAlarmSource("jammed")));

        // Act
        var errors = _sut.Validate(type);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.UnknownEvent, "alarms.blockedAlarm.source.event"));
    }

    [Fact]
    public void Validate_RuleAlarmOnUnknownProperty_ReportsUnknownProperty()
    {
        // Arrange
        var type = WithAlarm("highLimit", new AlarmDef(Severity(), "High", new RuleAlarmSource("pressure", AlarmCondition.Above)));

        // Act
        var errors = _sut.Validate(type);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.UnknownProperty, "alarms.highLimit.source.property"));
    }

    [Fact]
    public void Validate_UndeclaredFeatureOnPropertyAndCommand_ReportsUnknownFeatureForBoth()
    {
        // Arrange
        var level = Level();
        var type = level with
        {
            Properties = new Dictionary<string, PropertyDef>(level.Properties)
            {
                ["height"] = new(new NumberType("m"), PropertyCategory.State, Feature: "height")
            },
            Commands = new Dictionary<string, CommandDef>(level.Commands)
            {
                ["measure"] = new(Completion.Ack, TimeSpan.FromSeconds(5), Feature: "height")
            }
        };

        // Act
        var errors = _sut.Validate(type);

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.UnknownFeature, "properties.height.feature"),
            (ValidationCodes.UnknownFeature, "commands.measure.feature"));
    }

    [Fact]
    public void Validate_DataTypesOfParametersPayloadsAndAlarmParameters_AreChecked()
    {
        // Arrange
        var level = Level();
        var type = level with
        {
            Commands = new Dictionary<string, CommandDef>(level.Commands)
            {
                ["setMode"] = new(Completion.Ack, TimeSpan.FromSeconds(5), new Dictionary<string, DataType> { ["mode"] = new EnumType([]) })
            },
            Events = new Dictionary<string, EventDef> { ["blocked"] = new(new NumberType(Step: 0)) },
            Alarms = new Dictionary<string, AlarmDef>
            {
                ["highLimit"] = new(
                    Severity(),
                    "High",
                    new RuleAlarmSource("level", AlarmCondition.Above),
                    new Dictionary<string, AlarmParameter> { ["limit"] = new(new NumberType(Minimum: 1, Maximum: 0)) })
            }
        };

        // Act
        var errors = _sut.Validate(type);

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.EmptyEnum, "commands.setMode.parameters.mode.values"),
            (ValidationCodes.InvalidStep, "events.blocked.payload.step"),
            (ValidationCodes.InvalidRange, "alarms.highLimit.parameters.limit.dataType.minimum"));
    }

    [Fact]
    public void Validate_NullEntriesInMapsAndLists_ReportsNullEntry()
    {
        // Arrange
        var level = Level();
        var type = level with
        {
            Properties = new Dictionary<string, PropertyDef>(level.Properties) { ["speed"] = null! },
            Alarms = new Dictionary<string, AlarmDef> { ["highLimit"] = new(Severity(), "High", null!) },
            Features = ["hsv", null!]
        };

        // Act
        var errors = _sut.Validate(type);

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.NullEntry, "features[1]"),
            (ValidationCodes.NullEntry, "properties.speed"),
            (ValidationCodes.NullEntry, "alarms.highLimit.source"));
    }

    [Fact]
    public void Validate_NullMandatoryMap_ReportsNullEntryAndChecksTheRest()
    {
        // Arrange
        var type = Level() with { Commands = null! };

        // Act
        var errors = _sut.Validate(type);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, "commands"));
    }

    private static Contracts.Primitives.Severity Severity() => Contracts.Primitives.Severity.Warning;

    private static CapabilityType WithCommand(string name, CommandDef command)
    {
        var level = Level();

        return level with { Commands = new Dictionary<string, CommandDef>(level.Commands) { [name] = command } };
    }

    private static CapabilityType WithAlarm(string name, AlarmDef alarm)
    {
        var level = Level();

        return level with { Alarms = new Dictionary<string, AlarmDef>(level.Alarms) { [name] = alarm } };
    }
}
