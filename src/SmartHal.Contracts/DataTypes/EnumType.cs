namespace SmartHal.Contracts.DataTypes;

/// <summary>
/// Describes a value from a closed list of strings.
/// </summary>
/// <remarks>
/// <para>
/// A minor version of a schema type may add values, so consumers have to tolerate values they do not know.
/// </para>
/// <para>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </para>
/// </remarks>
/// <param name="Values">The permitted values, for example <c>online</c>, <c>degraded</c> and <c>offline</c>.</param>
public sealed record EnumType(IReadOnlyList<string> Values) : DataType;
