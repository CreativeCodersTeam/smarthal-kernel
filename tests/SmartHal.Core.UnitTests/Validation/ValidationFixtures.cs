using System.Text.Json.Nodes;
using SmartHal.Contracts.Api;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Integration;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;
using SmartHal.Contracts.Topology;
using SmartHal.Core.Abstractions.Validation;

namespace SmartHal.Core.UnitTests.Validation;

/// <summary>
/// Builds a small, valid set of schema types and a device that uses them; every test derives its invalid variant
/// from these with <c>with</c> expressions.
/// </summary>
internal static class ValidationFixtures
{
    public static readonly TypeRef LevelRef = new TypeRef("core.level", 1);

    public static readonly TypeRef DimmerProfileRef = new TypeRef("core.profile.dimmer", 1);

    public static readonly TypeRef DimmerTypeRef = new TypeRef("acme.dimmer", 1);

    public static readonly TypeRef HsvRef = new TypeRef("core.types.hsv", 1);

    /// <summary>
    /// Returns <c>core.level</c> 1.2 with a feature, a struct property, a confirmed and a result command, an event and
    /// one rule and one device alarm.
    /// </summary>
    public static CapabilityType Level() =>
        new CapabilityType("core.level", new TypeVersion(1, 2),
            new Dictionary<string, PropertyDef>
            {
                ["level"] = new PropertyDef(new NumberType("%", 0, 100, 1), PropertyCategory.State),
                ["color"] = new PropertyDef(new RefType(HsvRef), PropertyCategory.State, Feature: "hsv")
            },
            new Dictionary<string, CommandDef>
            {
                ["setLevel"] = new CommandDef(
                    Completion.Confirmed, TimeSpan.FromSeconds(30),
                    new Dictionary<string, DataType> { ["level"] = new NumberType("%"), ["transition"] = new DurationType() },
                    Affects: ["level"], RequiredParameters: ["level"]),
                ["calibrate"] = new CommandDef(Completion.Result, TimeSpan.FromSeconds(10), Result: new BooleanType())
            }, new Dictionary<string, EventDef> { ["blocked"] = new EventDef() },
            new Dictionary<string, AlarmDef>
            {
                ["highLimit"] =
                    new AlarmDef(Severity.Warning, "Level above limit", new RuleAlarmSource("level", AlarmCondition.Above),
                        new Dictionary<string, AlarmParameter> { ["limit"] = new AlarmParameter(new NumberType("%")) }),
                ["blockedAlarm"] = new AlarmDef(Severity.Major, "Blocked", new DeviceAlarmSource("blocked"))
            }, ["hsv"]);

    public static DataTypeDef Hsv() =>
        new DataTypeDef("core.types.hsv", new TypeVersion(1, 0),
            new ObjectType(
                new Dictionary<string, DataType>
                {
                    ["h"] = new NumberType("deg", 0, 360), ["s"] = new NumberType("%", 0, 100), ["v"] = new NumberType("%", 0, 100)
                }, ["h", "s", "v"]));

    public static ChannelProfile DimmerProfile() =>
        new ChannelProfile("core.profile.dimmer", new TypeVersion(1, 0), [new ProfileCapability(LevelRef, Required: true, Min: 1, Max: 2)]);

    public static DeviceType Dimmer() =>
        new DeviceType("acme.dimmer", new TypeVersion(1, 0), "Acme", "Dimmer",
            [new ChannelTemplate("0", []), new ChannelTemplate("1", [new CapabilityTemplate("level", LevelRef)], DimmerProfileRef)],
            BindingTemplates:
            [
                new BindingTemplate(Protocols.Zigbee, new Dictionary<string, DataType> { ["ieeeAddr"] = new StringType() },
                    [new Mapping("1/level/level", "${ieeeAddr}/0x0008/0x0000")])
            ]);

    public static Capability LevelCapability() =>
        new Capability(Guid.NewGuid(), "level", LevelRef, new TypeVersion(1, 2), ["hsv"], Guid.NewGuid(),
            new Dictionary<string, HistoryPolicy> { ["level"] = new HistoryPolicy(TimeSpan.FromDays(7)) },
            new Dictionary<string, IReadOnlyDictionary<string, JsonNode?>>
            {
                ["highLimit"] = new Dictionary<string, JsonNode?> { ["limit"] = 80 }
            });

    public static Device DimmerDevice(params Capability[] levelChannelCapabilities) =>
        new Device(Guid.NewGuid(), "flur.dimmer", "Flur Dimmer", Virtual: false, DeviceLifecycle.Active,
        [
            new Channel(Guid.NewGuid(), "0", []),
            new Channel(Guid.NewGuid(), "1", levelChannelCapabilities.Length == 0 ? [LevelCapability()] : levelChannelCapabilities,
                DimmerProfileRef)
        ], DimmerTypeRef);

    /// <summary>
    /// Returns a valid catalog with <c>core.level</c> 1.2 and 2.0, the profile, the device type, the hsv type and a
    /// migration from major 1 to 2.
    /// </summary>
    public static TypeCatalog TestCatalog() =>
        new TypeCatalog([Level(), Level() with { Version = new TypeVersion(2, 0) }], [DimmerProfile()], [Dimmer()], [Hsv()],
            [new CapabilityMigration(LevelRef, new TypeRef("core.level", 2))]);

    /// <summary>
    /// Returns <see cref="TestCatalog"/> with <c>core.level</c> 1.0 in front of 1.2. As a catalog it is invalid (the
    /// same type and major twice); it serves to show which minor version a reference resolves to.
    /// </summary>
    public static TypeCatalog TestCatalogWithTwoMinors()
    {
        var catalog = TestCatalog();

        return catalog with { Capabilities = [Level() with { Version = new TypeVersion(1, 0) }, .. catalog.Capabilities] };
    }

    /// <summary>
    /// Reduces violations to code and path, the two parts every test asserts exactly.
    /// </summary>
    public static IEnumerable<(string Code, string Path)> CodesAndPaths(this IReadOnlyList<ValidationError> errors) =>
        errors.Select(error => (error.Code, error.Path));
}
