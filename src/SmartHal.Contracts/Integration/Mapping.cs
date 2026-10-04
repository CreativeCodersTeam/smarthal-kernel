using System.Text.Json.Nodes;
using SmartHal.Contracts.Integration.Transforms;

namespace SmartHal.Contracts.Integration;

/// <summary>
/// Maps a protocol address to an element of a capability.
/// </summary>
/// <example>
/// A Modbus pressure register polled every second, scaled from hundredths of a bar:
/// <code language="csharp">
/// var mapping = new Mapping(
///     "hydraulik/druckseite/value",
///     "unit=${slaveId};hr=40002",
///     [new ScaleStep(0.01)],
///     [JsonValue.Create(65535)],
///     TimeSpan.FromSeconds(1));
/// </code>
/// </example>
/// <param name="Target">The capability element in the form <c>&lt;channelKey&gt;/&lt;capabilityKey&gt;/&lt;element&gt;</c>.</param>
/// <param name="Address">The protocol-specific address; it may contain <c>${placeholder}</c> parameters.</param>
/// <param name="Transform">The chain of value conversions; <see langword="null"/> when the value is taken as is.</param>
/// <param name="InvalidValues">The raw values that mean "no value"; <see langword="null"/> when there are none.</param>
/// <param name="PollInterval">The polling interval for protocols without push; <see langword="null"/> when the device pushes.</param>
/// <param name="Stateful">The handler that combines sequences of protocol events; <see langword="null"/> when none is needed.</param>
public sealed record Mapping(
    string Target,
    string Address,
    IReadOnlyList<TransformStep>? Transform = null,
    IReadOnlyList<JsonNode?>? InvalidValues = null,
    TimeSpan? PollInterval = null,
    StatefulHandler? Stateful = null);
