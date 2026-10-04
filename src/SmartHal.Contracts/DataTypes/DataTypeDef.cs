using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.DataTypes;

/// <summary>
/// Defines a reusable, versioned data type that other data types refer to through a <see cref="RefType"/>.
/// </summary>
/// <param name="Name">The namespaced name of the type, for example <c>core.types.hsv</c>.</param>
/// <param name="Version">The version of the type.</param>
/// <param name="DataType">The data type the definition stands for.</param>
public sealed record DataTypeDef(string Name, TypeVersion Version, DataType DataType);
