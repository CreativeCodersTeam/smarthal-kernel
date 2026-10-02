namespace SmartHal.Contracts.DataTypes;

/// <summary>
/// Describes a struct: a set of named fields, each with its own data type.
/// </summary>
/// <remarks>
/// <para>
/// Units are attached per field. A struct is at most two levels deep; for the history it is split into its fields,
/// for example <c>color.h</c>, <c>color.s</c> and <c>color.v</c>.
/// </para>
/// <para>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </para>
/// </remarks>
/// <param name="Fields">The fields of the struct, keyed by field name.</param>
/// <param name="Required">The names of the fields that must be present; <see langword="null"/> when every field is optional.</param>
public sealed record ObjectType(IReadOnlyDictionary<string, DataType> Fields, IReadOnlyList<string>? Required = null) : DataType;
