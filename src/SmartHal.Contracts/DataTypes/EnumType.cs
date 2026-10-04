namespace SmartHal.Contracts.DataTypes;

/// <summary>
/// Describes a value from a closed list of strings.
/// </summary>
/// <remarks>Consumers have to tolerate unknown values; a minor version of a schema type may add values.</remarks>
/// <param name="Values">The permitted values, for example <c>online</c>, <c>degraded</c> and <c>offline</c>.</param>
public sealed record EnumType(IReadOnlyList<string> Values) : DataType;
