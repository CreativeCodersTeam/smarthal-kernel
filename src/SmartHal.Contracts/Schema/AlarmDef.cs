using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Schema;

/// <summary>
/// Defines an alarm of a capability type.
/// </summary>
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
