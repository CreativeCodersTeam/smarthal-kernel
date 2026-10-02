# Formales Schema (C#)

Dieselben Typen wie im Tab mit dem TypeScript-Schema, als C#-Records für .NET 8.

## Konventionen

Der Code zielt auf .NET 8 (C# 12) und braucht nur `System.Text.Json`, keine NuGet-Pakete. Er kompiliert mit .NET SDK 8.0.131 ohne Warnungen; ein Round-Trip-Test erzeugt dasselbe JSON wie das TypeScript-Schema.

| TypeScript | C# |
| --- | --- |
| Interface | unveränderlicher, positionaler `record` |
| Union mit Diskriminator (`type`, `kind`, `fn`, `pattern`) | abstrakter Basis-Record + `[JsonPolymorphic]` mit denselben Diskriminatoren |
| String-Union (geschlossen) | `enum`, als snake\_case serialisiert (`ActiveUnacked` → `"active_unacked"`) |
| String-Union (erweiterbar: QualityReason, protocol) | `string` + Konstantenklasse |
| `Value` | `JsonNode?` |
| `UUID` · `Timestamp` · `Duration` | `Guid` · `DateTimeOffset` · `TimeSpan` (als ISO 8601, z. B. `"PT30S"`) |
| `TypeRef` · `TypeVersion` | `readonly record struct` mit eigenem Konverter (`"core.pressure@1"`, `"1.2"`) |

**Drei bewusste Abweichungen vom TypeScript-Schema**

- `integer | number` wird zu `IntegerType` und `NumberType` mit gemeinsamer Basis `NumericType`.
- `Address | KeyAddress` wird zu `ElementRef` mit Diskriminator `"by"`: `"id"` oder `"key"`.
- `Subscription` wird zu `IAsyncEnumerable<SubscriptionItem>`; beendet wird über das `CancellationToken`.

Alle Serialisierungen laufen über `IotJson.Options`.

## Grundtypen

IDs, Typreferenzen, Datentypen und die gemeinsamen Serializer-Optionen mit ihren Konvertern.

```javascript
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

namespace Iot.Model;

/// <summary>Referenz auf die Major-Version eines Typs. JSON: "core.pressure@1"</summary>
[JsonConverter(typeof(TypeRefConverter))]
public readonly record struct TypeRef(string Name, int Major)
{
    public override string ToString() => $"{Name}@{Major}";
    public static TypeRef Parse(string s)
    {
        var i = s.LastIndexOf('@');
        return new(s[..i], int.Parse(s[(i + 1)..]));
    }
}

/// <summary>Major.Minor. JSON: "1.2"</summary>
[JsonConverter(typeof(TypeVersionConverter))]
public readonly record struct TypeVersion(int Major, int Minor)
{
    public override string ToString() => $"{Major}.{Minor}";
    public static TypeVersion Parse(string s)
    {
        var p = s.Split('.');
        return new(int.Parse(p[0]), int.Parse(p[1]));
    }
}

/// <summary>Jedes Instanzobjekt: unveränderliche UUID + lesbarer Key.</summary>
public interface IIdentified
{
    Guid Id { get; }
    string Key { get; }                                  // eindeutig im Rahmen, umbenennbar
    IReadOnlyList<string>? Aliases { get; }              // frühere Keys, befristet gültig
    IReadOnlyDictionary<string, string>? Tags { get; }   // frei, vom Kern nicht geprüft
}

public enum Severity { Info, Warning, Minor, Major, Critical }

/// <summary>Datentypen: Teilmenge von JSON Schema, Diskriminator "type".</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(BooleanType), "boolean")]
[JsonDerivedType(typeof(IntegerType), "integer")]
[JsonDerivedType(typeof(NumberType), "number")]
[JsonDerivedType(typeof(StringType), "string")]
[JsonDerivedType(typeof(EnumType), "enum")]
[JsonDerivedType(typeof(TimestampType), "timestamp")]
[JsonDerivedType(typeof(DurationType), "duration")]
[JsonDerivedType(typeof(ObjectType), "object")]
[JsonDerivedType(typeof(ArrayType), "array")]
[JsonDerivedType(typeof(RefType), "ref")]
public abstract record DataType;

public sealed record BooleanType : DataType;
public abstract record NumericType(string? Unit, double? Minimum, double? Maximum, double? Step) : DataType;
public sealed record IntegerType(string? Unit = null, double? Minimum = null, double? Maximum = null, double? Step = null)
    : NumericType(Unit, Minimum, Maximum, Step);
public sealed record NumberType(string? Unit = null, double? Minimum = null, double? Maximum = null, double? Step = null)
    : NumericType(Unit, Minimum, Maximum, Step);         // Unit = UCUM, z. B. "bar"
public sealed record StringType(int? MaxLength = null, string? Pattern = null) : DataType;
public sealed record EnumType(IReadOnlyList<string> Values) : DataType;   // Minor-Versionen dürfen Werte ergänzen
public sealed record TimestampType : DataType;
public sealed record DurationType : DataType;
public sealed record ObjectType(IReadOnlyDictionary<string, DataType> Fields, IReadOnlyList<string>? Required = null)
    : DataType;                                          // höchstens 2 Ebenen tief
public sealed record ArrayType(DataType Items, int? MaxItems = null) : DataType;
public sealed record RefType(TypeRef Ref) : DataType;    // wiederverwendbar, z. B. "core.types.hsv@1"

// Werte (Value im TypeScript-Schema) sind JsonNode?: bool, Zahl, String, null, Array oder Objekt.

/// <summary>Einheitliche Serializer-Optionen: camelCase, Enums snake_case, Dauer ISO 8601.</summary>
public static class IotJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),  // ActiveUnacked → "active_unacked"
            new IsoDurationConverter(),
        },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

public sealed class TypeRefConverter : JsonConverter<TypeRef>
{
    public override TypeRef Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => TypeRef.Parse(r.GetString()!);
    public override void Write(Utf8JsonWriter w, TypeRef v, JsonSerializerOptions o) => w.WriteStringValue(v.ToString());
}

public sealed class TypeVersionConverter : JsonConverter<TypeVersion>
{
    public override TypeVersion Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => TypeVersion.Parse(r.GetString()!);
    public override void Write(Utf8JsonWriter w, TypeVersion v, JsonSerializerOptions o) => w.WriteStringValue(v.ToString());
}

/// <summary>TimeSpan als ISO 8601 ("PT30S") statt "00:00:30".</summary>
public sealed class IsoDurationConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => XmlConvert.ToTimeSpan(r.GetString()!);
    public override void Write(Utf8JsonWriter w, TimeSpan v, JsonSerializerOptions o) => w.WriteStringValue(XmlConvert.ToString(v));
}
```

## Schema-Ebene

Properties sind immer read-only; geändert wird nur über Commands.

```javascript
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Iot.Model;

public enum PropertyCategory { State, Config, Diagnostic }
public enum Aggregation { Any, All, Avg, Min, Max, Sum, Last }
public enum RollupAggregate { Min, Max, Avg, Last }
public enum Completion { Ack, Confirmed, Result }

public sealed record CapabilityType(
    string Name,                                         // "core.pressure", "vendor.acme.filter"
    TypeVersion Version,
    IReadOnlyDictionary<string, PropertyDef> Properties,
    IReadOnlyDictionary<string, CommandDef> Commands,
    IReadOnlyDictionary<string, EventDef> Events,
    IReadOnlyDictionary<string, AlarmDef> Alarms,
    IReadOnlyList<string>? Features = null);             // z. B. ["hsv", "ct"]

public sealed record PropertyDef(
    DataType DataType,
    PropertyCategory Category,                           // immer read-only; Config ⇒ set<Property> wird abgeleitet
    bool? DeriveSetter = null,                           // für State optional
    string? Feature = null,                              // nur vorhanden, wenn Feature aktiv
    HistoryPolicy? History = null,                       // Default, Instanz darf überschreiben
    Aggregation? Aggregation = null);                    // Default für virtuelle Devices

public sealed record HistoryPolicy(
    TimeSpan RawRetention,
    IReadOnlyList<Rollup>? Rollups = null,
    Deadband? Deadband = null);

public sealed record Rollup(TimeSpan Interval, IReadOnlyList<RollupAggregate> Aggregates, TimeSpan Retention);
public sealed record Deadband(double? Absolute = null, double? Relative = null, TimeSpan? MinInterval = null);

public sealed record CommandDef(
    Completion Completion,
    TimeSpan Timeout,
    IReadOnlyDictionary<string, DataType>? Parameters = null,
    DataType? Result = null,                             // Pflicht bei Completion.Result
    IReadOnlyList<string>? Affects = null,               // Property-Namen dieser Capability
    string? Feature = null);

public sealed record EventDef(DataType? Payload = null, Severity? Severity = null);

public enum AlarmCondition { Above, Below, Equals, Stale }

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(DeviceAlarmSource), "device")]
[JsonDerivedType(typeof(RuleAlarmSource), "rule")]
public abstract record AlarmSource;
public sealed record DeviceAlarmSource(string Event) : AlarmSource;          // EventDef, das den Alarm meldet
public sealed record RuleAlarmSource(string Property, AlarmCondition Condition) : AlarmSource;

public sealed record AlarmParameter(DataType DataType, JsonNode? Default);

public sealed record AlarmDef(
    Severity Severity,
    string Message,
    AlarmSource Source,
    IReadOnlyDictionary<string, AlarmParameter>? Parameters = null);   // limit, delay, hysteresis

public sealed record CapabilityMigration(
    TypeRef From,                                        // "core.level@1"
    TypeRef To,                                          // "core.level@2"
    IReadOnlyDictionary<string, string?>? Properties = null,   // alt → neu, null = entfällt
    IReadOnlyDictionary<string, string?>? Commands = null,
    IReadOnlyDictionary<string, string?>? Events = null);

public sealed record ProfileCapability(TypeRef Type, bool Required, int? Min = null, int? Max = null);

public sealed record ChannelProfile(
    string Name,                                         // "core.profile.dimmablelight"
    TypeVersion Version,
    IReadOnlyList<ProfileCapability> Capabilities);

public sealed record SleepyConfig(TimeSpan WakeInterval);
public sealed record CapabilityTemplate(string Key, TypeRef Type, IReadOnlyList<string>? Features = null);
public sealed record ChannelTemplate(
    string Key,                                          // "0" = Root
    IReadOnlyList<CapabilityTemplate> Capabilities,
    TypeRef? Profile = null);

public sealed record DeviceType(
    string Name,                                         // "acme.trv2"
    TypeVersion Version,
    string Manufacturer,
    string Model,
    IReadOnlyList<ChannelTemplate> Channels,
    SleepyConfig? Sleepy = null,
    IReadOnlyList<BindingTemplate>? BindingTemplates = null);   // eine Vorlage je Protokoll
```

## Instanz-Ebene

Jede Capability zeigt auf genau ein Binding; ein DiscoveryResult zeigt auf den Adapter, der es gefunden hat.

```csharp
using System.Text.Json.Nodes;

namespace Iot.Model;

public enum LocationKind { Site, Building, Floor, Room, Zone }
public enum DeviceLifecycle { Provisioned, Active, Decommissioned }
public enum DiscoveryStatus { New, Approved, Rejected, Ignored }

public sealed record Location(
    Guid Id, string Key, string Name, LocationKind Kind,
    Guid? ParentId = null,
    IReadOnlyList<string>? Aliases = null, IReadOnlyDictionary<string, string>? Tags = null) : IIdentified;

public sealed record Device(
    Guid Id,
    string Key,                                          // global eindeutig: "halle2.pumpe3"
    string Name,
    bool Virtual,
    DeviceLifecycle Lifecycle,
    IReadOnlyList<Channel> Channels,                     // enthält immer Channel "0" (Root)
    TypeRef? TypeRef = null,                             // DeviceType; bei virtuellen Devices optional
    Guid? LocationId = null,
    Guid? ConnectedVia = null,                           // Gateway-Device
    IReadOnlyList<string>? Aliases = null, IReadOnlyDictionary<string, string>? Tags = null) : IIdentified;

public sealed record Channel(
    Guid Id,
    string Key,                                          // eindeutig im Device
    IReadOnlyList<Capability> Capabilities,
    TypeRef? Profile = null,
    Guid? LocationId = null,                             // Override des Device-Orts
    IReadOnlyList<string>? Aliases = null, IReadOnlyDictionary<string, string>? Tags = null) : IIdentified;

public sealed record Capability(
    Guid Id,
    string Key,                                          // eindeutig im Channel: "druckseite"
    TypeRef TypeRef,
    TypeVersion Version,                                 // tatsächlich implementierte Minor-Version
    IReadOnlyList<string> Features,
    Guid BindingId,                                      // genau eine Quelle
    IReadOnlyDictionary<string, HistoryPolicy>? HistoryOverrides = null,                        // Property → Policy
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, JsonNode?>>? AlarmParameters = null, // AlarmDef → Parameter → Wert
    IReadOnlyList<string>? Aliases = null, IReadOnlyDictionary<string, string>? Tags = null) : IIdentified;

public sealed record DiscoveryResult(
    Guid Id,
    Guid AdapterId,                                      // welcher Adapter hat gefunden
    string Address,                                      // Protokolladresse
    IReadOnlyDictionary<string, JsonNode?> Parameters,   // Binding-Parameter, z. B. ieeeAddr
    DiscoveryStatus Status,
    DateTimeOffset DiscoveredAt,
    TypeRef? SuggestedType = null);
```

## Laufzeit und Bus

Alle Laufzeitobjekte nutzen dieselbe Adresse. `ElementRef` erlaubt Adressierung per UUIDs oder per Keys.

```javascript
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Iot.Model;

/// <summary>Adresse eines Elements: per UUIDs (Address) oder per Keys (KeyAddress).</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "by")]
[JsonDerivedType(typeof(Address), "id")]
[JsonDerivedType(typeof(KeyAddress), "key")]
public abstract record ElementRef;

public sealed record Address(Guid DeviceId, Guid ChannelId, Guid CapabilityId, string Element) : ElementRef
{
    [JsonIgnore] public CapabilityAddress Capability => new(DeviceId, ChannelId, CapabilityId);
}

public sealed record KeyAddress(string Device, string Channel, string Capability, string Element) : ElementRef;

public sealed record CapabilityAddress(Guid DeviceId, Guid ChannelId, Guid CapabilityId);

/// <summary>Erweiterbar, daher Konstanten statt Enum.</summary>
public static class QualityReasons
{
    public const string UpstreamOffline = "upstream_offline";   // Gateway offline
    public const string SourceOffline = "source_offline";       // eigenes Binding offline
    public const string InvalidValue = "invalid_value";         // Sonderwert vom Gerät
    public const string Stale = "stale";                        // länger keine Meldung
    public const string MemberBad = "member_bad";               // virtuelles Device: Mitglied bad
}

public enum QualityLevel { Good, Uncertain, Bad }
public sealed record Quality(QualityLevel Level, string? Reason = null);
public enum StateOrigin { Device, Virtual }

public sealed record PropertyState(
    Address Address,
    JsonNode? Value,                                     // null nur bei QualityLevel.Bad
    DateTimeOffset ReceivedTs,
    Quality Quality,
    StateOrigin Origin,
    long Seq,
    DateTimeOffset? SourceTs = null);                    // vom Gerät, falls vorhanden

public enum CommandStatus
{
    Pending, Sent, Acked,
    Completed, Partial, Failed, Timeout, Cancelled,
    Skipped,                                             // nur Kind-Invocations im Fan-out
}

public enum IssuerKind { User, Automation, System }
public sealed record Issuer(IssuerKind Kind, string Id);
public sealed record CommandError(string Code, string Message);
public sealed record StatusTransition(CommandStatus Status, DateTimeOffset At);

public sealed record CommandInvocation(
    Guid Id,                                             // = correlationId
    string IdempotencyKey,                               // vom Client, Duplikate → dieselbe Invocation
    Address Address,
    IReadOnlyDictionary<string, JsonNode?> Parameters,
    Issuer Issuer,
    CommandStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset Deadline,                             // Timeout, bei sleepy + Aufwachintervall
    IReadOnlyList<StatusTransition> Transitions,
    Guid? ParentId = null,                               // Fan-out
    JsonNode? Result = null,
    CommandError? Error = null);

public sealed record EventOccurrence(
    Guid Id,
    Address Address,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedTs,
    JsonNode? Payload = null,
    Severity? Severity = null);

public enum AlarmState { ActiveUnacked, ActiveAcked, ClearedUnacked, Cleared }
public enum AlarmTrigger { Device, Rule }
public sealed record AlarmTransition(AlarmState State, DateTimeOffset At, string? By = null);

public sealed record AlarmInstance(
    Guid Id,
    Address Address,                                     // Element = AlarmDef-Name
    AlarmTrigger Trigger,
    Severity Severity,
    AlarmState State,
    IReadOnlyList<AlarmTransition> Transitions,
    DateTimeOffset? ShelvedUntil = null);

// Zustellung at-least-once; Seq steigt pro Quelle; Reihenfolge pro Quelle garantiert.
// Transport-Abbildungen (z. B. MQTT-Topics) liegen außerhalb des Kerns.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(StateChanged), "StateChanged")]
[JsonDerivedType(typeof(EventOccurred), "EventOccurred")]
[JsonDerivedType(typeof(CommandUpdated), "CommandUpdated")]
public abstract record BusMessage(long Seq);
public sealed record StateChanged(long Seq, PropertyState State, JsonNode? Previous = null) : BusMessage(Seq);
public sealed record EventOccurred(long Seq, EventOccurrence Event) : BusMessage(Seq);   // auch Alarm-Übergänge
public sealed record CommandUpdated(long Seq, CommandInvocation Invocation) : BusMessage(Seq);
```

## Integration

Nur diese Typen enthalten Protokollwissen. Ein Transform läuft beim Lesen vorwärts und beim Schreiben rückwärts.

```javascript
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Iot.Model;

// Transform: vorwärts beim Lesen, rückwärts beim Schreiben. Diskriminator "fn".
[JsonPolymorphic(TypeDiscriminatorPropertyName = "fn")]
[JsonDerivedType(typeof(ScaleStep), "scale")]
[JsonDerivedType(typeof(OffsetStep), "offset")]
[JsonDerivedType(typeof(InvertStep), "invert")]
[JsonDerivedType(typeof(ReciprocalStep), "reciprocal")]
[JsonDerivedType(typeof(Log10Step), "log10")]
[JsonDerivedType(typeof(EnumMapStep), "enumMap")]
public abstract record TransformStep;
public sealed record ScaleStep(double Factor) : TransformStep;
public sealed record OffsetStep(double Value) : TransformStep;
public sealed record InvertStep(double Max) : TransformStep;           // x → max − x (cover: Matter 0 = offen)
public sealed record ReciprocalStep(double K) : TransformStep;         // x → k / x (Mired ↔ Kelvin, k = 1e6)
public sealed record Log10Step(double? Factor = null, double? Offset = null) : TransformStep;
public sealed record EnumMapStep(IReadOnlyDictionary<string, JsonNode?> Map) : TransformStep;

public sealed record StatefulHandler(string Handler, IReadOnlyDictionary<string, JsonNode?>? Config = null);

public sealed record Mapping(
    string Target,                                       // "<channelKey>/<capabilityKey>/<element>"
    string Address,                                      // protokollspezifisch, darf ${Platzhalter} enthalten
    IReadOnlyList<TransformStep>? Transform = null,
    IReadOnlyList<JsonNode?>? InvalidValues = null,      // → QualityLevel.Bad / invalid_value
    TimeSpan? PollInterval = null,                       // nur bei Protokollen ohne Push
    StatefulHandler? Stateful = null);                   // Event-Folgen zusammenfassen

/// <summary>Erweiterbar, daher Konstanten statt Enum.</summary>
public static class Protocols
{
    public const string Matter = "matter", Zigbee = "zigbee", Modbus = "modbus", OpcUa = "opcua", Mqtt = "mqtt";
}

public sealed record BindingTemplate(
    string Protocol,
    IReadOnlyDictionary<string, DataType> Parameters,    // z. B. slaveId, ieeeAddr
    IReadOnlyList<Mapping> Mappings);

public enum BindingKind { Protocol, Internal }
public enum BindingState { Online, Offline, Error }
public sealed record BindingStatus(BindingState State, DateTimeOffset? LastSeen = null);
public sealed record TemplateRef(TypeRef DeviceType, string Protocol);

/// <summary>Zuordnung eines Devices zu einem Adapter (oder interne Logik bei virtuellen Devices).</summary>
public sealed record Binding(
    Guid Id,
    Guid DeviceId,
    BindingKind Kind,
    IReadOnlyDictionary<string, JsonNode?> Parameters,
    BindingStatus Status,
    Guid? AdapterId = null,                              // nur bei BindingKind.Protocol
    TemplateRef? Template = null,
    IReadOnlyList<Mapping>? Overrides = null,            // markiert, überleben Template-Updates
    InternalBinding? Internal = null);                   // nur bei BindingKind.Internal

public sealed record MemberCall(string Command, IReadOnlyDictionary<string, JsonNode?>? Parameters = null);

// Ersatzregeln: feste Muster, keine Ausdruckssprache. Diskriminator "pattern".
[JsonPolymorphic(TypeDiscriminatorPropertyName = "pattern")]
[JsonDerivedType(typeof(ThresholdSubstitution), "threshold")]
[JsonDerivedType(typeof(EnumMapSubstitution), "enumMap")]
[JsonDerivedType(typeof(FixedSubstitution), "fixed")]
public abstract record Substitution(string Command, TypeRef MemberType);
public sealed record ThresholdSubstitution(
    string Command, TypeRef MemberType, string Param, double Above, MemberCall Then, MemberCall Else)
    : Substitution(Command, MemberType);
public sealed record EnumMapSubstitution(
    string Command, TypeRef MemberType, string Param, IReadOnlyDictionary<string, MemberCall> Map)
    : Substitution(Command, MemberType);
public sealed record FixedSubstitution(string Command, TypeRef MemberType, MemberCall Use)
    : Substitution(Command, MemberType);

public sealed record InternalBinding(                    // virtuelle Devices
    IReadOnlyList<CapabilityAddress> Members,
    IReadOnlyDictionary<string, Aggregation>? Aggregations = null,   // Property → Override des Defaults
    IReadOnlyList<Substitution>? Substitutions = null);              // ohne Treffer → CommandStatus.Skipped

public enum AdapterStatus { Running, Stopped, Error }
public enum ConnectionStatus { Connected, Disconnected, Error }
public sealed record DiscoverySettings(bool Enabled);    // legt DiscoveryResults in die Inbox

/// <summary>Ein Protokoll, z. B. Zigbee-Adapter: Connections, Discovery.</summary>
public sealed record Adapter(
    Guid Id,
    string Key,
    string Protocol,
    IReadOnlyList<Connection> Connections,
    DiscoverySettings Discovery,
    AdapterStatus Status);

public sealed record Connection(
    Guid Id,
    Guid AdapterId,
    string Endpoint,                                     // Broker-URL, Serial-Port, OPC-UA-Endpoint
    ConnectionStatus Status);
```

## API zur Automationsschicht

Die API ist transportneutral. Ein Abo liefert erst einen Snapshot, dann Nachrichten, jeweils mit Cursor; beendet wird es über das `CancellationToken`.

```csharp
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Iot.Model;

public sealed record Filter(
    Guid? LocationId = null,                             // inkl. Teilbaum
    string? KeyPattern = null,                           // "halle2.*"
    IReadOnlyList<TypeRef>? CapabilityTypes = null,
    IReadOnlyDictionary<string, string>? Tags = null,
    IReadOnlyList<string>? Kinds = null);                // "StateChanged" | "EventOccurred" | "CommandUpdated"

public sealed record HistoryPoint(
    DateTimeOffset Ts,
    QualityLevel Quality,
    JsonNode? Value = null,                              // Rohwert
    double? Min = null, double? Max = null, double? Avg = null);   // Rollup

public sealed record InvokeRequest(
    ElementRef Address,                                  // Address (UUIDs) oder KeyAddress (Keys)
    IReadOnlyDictionary<string, JsonNode?> Parameters,
    Issuer Issuer,
    string IdempotencyKey);                              // Pflicht

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SnapshotItem), "snapshot")]
[JsonDerivedType(typeof(MessageItem), "message")]
[JsonDerivedType(typeof(ResyncItem), "resync")]
public abstract record SubscriptionItem;
public sealed record SnapshotItem(IReadOnlyList<PropertyState> States, string Cursor) : SubscriptionItem;
public sealed record MessageItem(BusMessage Message, string Cursor) : SubscriptionItem;
public sealed record ResyncItem(string Reason) : SubscriptionItem;   // "cursor_expired" → neuer Snapshot folgt

public sealed record TypeCatalog(
    IReadOnlyList<CapabilityType> Capabilities,
    IReadOnlyList<ChannelProfile> Profiles,
    IReadOnlyList<DeviceType> DeviceTypes);

public interface ICoreApi
{
    // Modell lesen
    Task<IReadOnlyList<Device>> ListDevicesAsync(Filter? filter = null, CancellationToken ct = default);
    Task<Device> GetDeviceAsync(string idOrKey, CancellationToken ct = default);
    Task<TypeCatalog> GetTypesAsync(CancellationToken ct = default);

    // Zustand lesen
    Task<IReadOnlyList<PropertyState>> GetStateAsync(Filter filter, CancellationToken ct = default);
    Task<IReadOnlyList<HistoryPoint>> GetHistoryAsync(
        ElementRef address, DateTimeOffset from, DateTimeOffset to, TimeSpan? rollup = null, CancellationToken ct = default);
    Task<IReadOnlyList<AlarmInstance>> ListAlarmsAsync(
        Filter? filter = null, IReadOnlyList<AlarmState>? states = null, CancellationToken ct = default);

    // Abonnieren: erst Snapshot, dann Nachrichten; beenden per CancellationToken
    IAsyncEnumerable<SubscriptionItem> SubscribeAsync(Filter filter, string? since = null, CancellationToken ct = default);

    // Befehlen
    Task<CommandInvocation> InvokeAsync(InvokeRequest request, CancellationToken ct = default);
    Task<CommandInvocation> GetInvocationAsync(Guid id, CancellationToken ct = default);
    Task<CommandInvocation> CancelAsync(Guid id, CancellationToken ct = default);   // nur solange Pending
}
```

**Beispiel** (geprüft): Das Pumpen-Mapping aus dem TypeScript-Tab, in C# erzeugt und serialisiert.

```csharp
var m = new Mapping("hydraulik/druckseite/value", "unit=${slaveId};hr=40002",
    [new ScaleStep(0.01)], [JsonValue.Create(65535)], TimeSpan.FromSeconds(1));
var json = JsonSerializer.Serialize(m, IotJson.Options);
// {"target":"hydraulik/druckseite/value","address":"unit=${slaveId};hr=40002",
//  "transform":[{"fn":"scale","factor":0.01}],"invalidValues":[65535],"pollInterval":"PT1S"}
```
