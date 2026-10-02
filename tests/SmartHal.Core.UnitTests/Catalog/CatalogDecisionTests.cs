using AwesomeAssertions;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;
using SmartHal.Core.Catalog;
using Xunit;

namespace SmartHal.Core.UnitTests.Catalog;

/// <summary>
/// Verifies the decisions of the approved catalog value table: units, limit alarms, battery alarms, timeouts,
/// features, severities and the reusable color type.
/// </summary>
public sealed class CatalogDecisionTests
{
    private static readonly string[] CoverMovingCommands = ["open", "close", "setPosition"];

    public static TheoryData<string, string, string, Severity> MeasuredQuantities => new()
    {
        { "core.temperature", "value", "Cel", Severity.Warning },
        { "core.humidity", "value", "%", Severity.Warning },
        { "core.pressure", "value", "bar", Severity.Major },
        { "core.flow", "value", "m3/h", Severity.Major },
        { "core.rotationalspeed", "value", "/min", Severity.Major },
        { "core.filllevel", "value", "%", Severity.Major },
        { "core.vibration", "velocity", "mm/s", Severity.Major }
    };

    [Theory]
    [MemberData(nameof(MeasuredQuantities))]
    public void LimitAlarms_OfEveryMeasuredQuantity_AreHighAndLowRulesOnTheQuantity(
        string name,
        string property,
        string unit,
        Severity severity)
    {
        // Arrange
        var capability = Find(name);

        // Act
        var high = capability.Alarms["highLimit"];
        var low = capability.Alarms["lowLimit"];

        // Assert
        ((NumberType)capability.Properties[property].DataType).Unit.Should().Be(unit);
        high.Source.Should().Be(new RuleAlarmSource(property, AlarmCondition.Above));
        low.Source.Should().Be(new RuleAlarmSource(property, AlarmCondition.Below));
        high.Severity.Should().Be(severity);
        low.Severity.Should().Be(severity);
        var parameters = high.Parameters!;
        parameters.Keys.Should().BeEquivalentTo("limit", "delay", "hysteresis");
        ((NumberType)parameters["limit"].DataType).Unit.Should().Be(unit);
        parameters["limit"].Default.Should().BeNull("a limit alarm stays inactive until the instance sets its limit");
        parameters["delay"].DataType.Should().BeOfType<DurationType>();
        parameters["delay"].Default!.GetValue<string>().Should().Be("PT10S");
        parameters["hysteresis"].Default!.GetValue<double>().Should().Be(0);
    }

    [Fact]
    public void Battery_Always_HasARuleAndADeviceLowBatteryAlarm()
    {
        // Arrange
        var battery = SystemCapabilities.Battery;

        // Act
        var rule = battery.Alarms["batteryLow"];
        var device = battery.Alarms["batteryLowReported"];

        // Assert
        rule.Source.Should().Be(new RuleAlarmSource("level", AlarmCondition.Below));
        rule.Severity.Should().Be(Severity.Warning);
        rule.Parameters!["limit"].Default!.GetValue<double>().Should().Be(20);
        rule.Parameters["hysteresis"].Default!.GetValue<double>().Should().Be(5);
        rule.Parameters["delay"].Default!.GetValue<string>().Should().Be("PT0S");
        device.Source.Should().Be(new DeviceAlarmSource("lowBattery"));
        device.Severity.Should().Be(Severity.Warning);
        battery.Events.Keys.Should().Contain("lowBattery");
        battery.Properties["level"].Aggregation.Should().Be(Aggregation.Min);
    }

    [Fact]
    public void Cover_Always_OpensFullyAtOneHundredPercentWithinTwoMinutes()
    {
        // Arrange
        var cover = ActuatorCapabilities.Cover;

        // Act
        var position = (NumberType)cover.Properties["position"].DataType;

        // Assert
        position.Should().Be(new NumberType("%", 0, 100));
        CoverMovingCommands.Should().AllSatisfy(command =>
        {
            cover.Commands[command].Timeout.Should().Be(TimeSpan.FromSeconds(120));
            cover.Commands[command].Affects.Should().Equal("position");
        });
        cover.Commands["stop"].Affects.Should().Equal("motion");
        cover.Commands["stop"].Timeout.Should().Be(TimeSpan.FromSeconds(30));
        cover.Alarms["blocked"].Should().Be(new AlarmDef(Severity.Major, "The cover is blocked.", new DeviceAlarmSource("blocked")));
    }

    [Fact]
    public void RunState_Always_HasACriticalFaultAlarmAndOneMinuteStartStop()
    {
        // Arrange
        var runState = IiotCapabilities.RunState;

        // Act
        var fault = runState.Alarms["fault"];

        // Assert
        fault.Severity.Should().Be(Severity.Critical);
        fault.Source.Should().Be(new DeviceAlarmSource("fault"));
        runState.Commands["start"].Timeout.Should().Be(TimeSpan.FromSeconds(60));
        runState.Commands["stop"].Timeout.Should().Be(TimeSpan.FromSeconds(60));
        runState.Commands["resetFault"].Result.Should().BeOfType<BooleanType>();
        runState.Properties["runtimeHours"].Category.Should().Be(PropertyCategory.Diagnostic);
        runState.Properties["startCount"].Category.Should().Be(PropertyCategory.Diagnostic);
        ((EnumType)runState.Properties["state"].DataType).Values.Should().Equal("stopped", "starting", "running", "stopping", "fault");
    }

    [Fact]
    public void Lock_Always_HasAMajorJammedAlarm()
    {
        // Arrange
        var lockCapability = ActuatorCapabilities.Lock;

        // Act
        var jammed = lockCapability.Alarms["jammed"];

        // Assert
        jammed.Severity.Should().Be(Severity.Major);
        jammed.Source.Should().Be(new DeviceAlarmSource("jammed"));
    }

    [Fact]
    public void FillLevel_Always_OffersTheHeightFeatureInMetres()
    {
        // Arrange
        var fillLevel = IiotCapabilities.FillLevel;

        // Act
        var height = fillLevel.Properties["height"];

        // Assert
        fillLevel.Features.Should().Equal("height");
        height.Feature.Should().Be("height");
        ((NumberType)height.DataType).Unit.Should().Be("m");
    }

    [Fact]
    public void Color_Always_SplitsHsvAndColorTemperatureIntoFeatures()
    {
        // Arrange
        var color = ActuatorCapabilities.Color;

        // Act
        var temperature = (NumberType)color.Properties["colorTemperature"].DataType;

        // Assert
        color.Features.Should().Equal("hsv", "ct");
        color.Properties["color"].Feature.Should().Be("hsv");
        color.Properties["color"].DataType.Should().Be(new RefType(new TypeRef("core.types.hsv", 1)));
        color.Commands["setColor"].Feature.Should().Be("hsv");
        color.Properties["colorTemperature"].Feature.Should().Be("ct");
        color.Commands["setColorTemperature"].Feature.Should().Be("ct");
        temperature.Should().Be(new NumberType("K", 1000, 10000));
    }

    [Fact]
    public void Hsv_Always_HasThreeRequiredFieldsWithUnits()
    {
        // Arrange
        var hsv = CoreDataTypes.Hsv;

        // Act
        var fields = ((ObjectType)hsv.DataType).Fields;

        // Assert
        hsv.Name.Should().Be("core.types.hsv");
        hsv.Version.Should().Be(new TypeVersion(1, 0));
        fields["h"].Should().Be(new NumberType("deg", 0, 360));
        fields["s"].Should().Be(new NumberType("%", 0, 100));
        fields["v"].Should().Be(new NumberType("%", 0, 100));
        ((ObjectType)hsv.DataType).Required.Should().Equal("h", "s", "v");
    }

    [Fact]
    public void Timeouts_OfSpecialCommands_FollowTheApprovedTable()
    {
        // Arrange

        // Act
        var startUpdate = SystemCapabilities.Firmware.Commands["startUpdate"];

        // Assert
        startUpdate.Timeout.Should().Be(TimeSpan.FromHours(1));
        startUpdate.Result.Should().BeOfType<StringType>();
        SystemCapabilities.Alarms.Commands["acknowledge"].Timeout.Should().Be(TimeSpan.FromSeconds(5));
        SystemCapabilities.Alarms.Commands["shelve"].Timeout.Should().Be(TimeSpan.FromSeconds(5));
        SystemCapabilities.Alarms.Commands["shelve"].RequiredParameters.Should().Equal("alarmId", "duration");
        SystemCapabilities.Identify.Commands["identify"].Completion.Should().Be(Completion.Ack);
        SystemCapabilities.Identify.Commands["identify"].Timeout.Should().Be(TimeSpan.FromSeconds(10));
        ActuatorCapabilities.OnOff.Commands["toggle"].Timeout.Should().Be(TimeSpan.FromSeconds(30));
        IiotCapabilities.RunState.Commands["resetFault"].Timeout.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Connectivity_Always_RaisesAMajorAlarmWhenOfflineForFiveMinutes()
    {
        // Arrange
        var connectivity = SystemCapabilities.Connectivity;

        // Act
        var offline = connectivity.Alarms["offline"];

        // Assert
        offline.Severity.Should().Be(Severity.Major);
        offline.Source.Should().Be(new RuleAlarmSource("status", AlarmCondition.Equals));
        offline.Parameters!["value"].Default!.GetValue<string>().Should().Be("offline");
        offline.Parameters["delay"].Default!.GetValue<string>().Should().Be("PT5M");
        connectivity.Properties["linkQuality"].Category.Should().Be(PropertyCategory.Diagnostic);
        connectivity.Properties["linkQuality"].Aggregation.Should().Be(Aggregation.Min);
    }

    [Fact]
    public void Aggregations_OfTheDocumentedExamples_MatchTheDomainModel()
    {
        // Arrange

        // Act

        // Assert
        // Domain model, section "Aggregations-Default": onoff → any, level → avg, contact → any.
        ActuatorCapabilities.OnOff.Properties["on"].Aggregation.Should().Be(Aggregation.Any);
        ActuatorCapabilities.Level.Properties["level"].Aggregation.Should().Be(Aggregation.Avg);
        SensorCapabilities.Contact.Properties["open"].Aggregation.Should().Be(Aggregation.Any);
    }

    [Fact]
    public void Button_Always_ReportsThePressTypeInItsPayload()
    {
        // Arrange

        // Act
        var payload = (ObjectType)SensorCapabilities.Button.Events["pressed"].Payload!;

        // Assert
        SensorCapabilities.Button.Properties.Should().BeEmpty();
        ((EnumType)payload.Fields["type"]).Values.Should().Equal("single", "double", "long");
        payload.Required.Should().Equal("type");
    }

    private static CapabilityType Find(string name) => CoreCapabilityCatalog.All.Single(capability => capability.Name == name);
}
