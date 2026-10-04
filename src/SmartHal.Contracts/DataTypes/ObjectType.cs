namespace SmartHal.Contracts.DataTypes;

/// <summary>
/// Describes a struct: a set of named fields, each with its own data type.
/// </summary>
/// <param name="Fields">The fields of the struct, keyed by field name.</param>
/// <param name="Required">The names of the fields that must be present; <see langword="null"/> when every field is optional.</param>
public sealed record ObjectType(IReadOnlyDictionary<string, DataType> Fields, IReadOnlyList<string>? Required = null) : DataType;
