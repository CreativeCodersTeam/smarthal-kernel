using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Schema;

/// <summary>
/// Defines an event of a capability type.
/// </summary>
/// <param name="Payload">The data type of the payload; <see langword="null"/> when the event carries none.</param>
/// <param name="Severity">The severity of the event; <see langword="null"/> when it has none.</param>
public sealed record EventDef(DataType? Payload = null, Severity? Severity = null);
