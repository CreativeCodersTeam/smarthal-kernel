using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Schema;

/// <summary>
/// Defines an alarm of a capability type.
/// </summary>
/// <remarks>
/// <para>
/// A raised alarm becomes an alarm instance with a lifecycle after ISA-18.2; acknowledging and shelving go through
/// the commands of <c>core.alarms</c>.
/// </para>
/// <para>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </para>
/// </remarks>
/// <param name="Severity">The severity of the alarm.</param>
/// <param name="Message">The text shown for the alarm.</param>
/// <param name="Source">What raises the alarm.</param>
/// <param name="Parameters">The parameters of a rule alarm, for example <c>limit</c>, <c>delay</c> and <c>hysteresis</c>; <see
/// langword="null"/> when there are none.</param>
public sealed record AlarmDef(
    Severity Severity,
    string Message,
    AlarmSource Source,
    IReadOnlyDictionary<string, AlarmParameter>? Parameters = null);
