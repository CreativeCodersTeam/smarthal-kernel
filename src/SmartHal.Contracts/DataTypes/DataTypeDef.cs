using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.DataTypes;

/// <summary>
/// Defines a reusable data type as a schema element of its own, with a namespaced name and a version.
/// </summary>
/// <remarks>
/// Other data types refer to it through a <see cref="RefType"/>, for example to <c>core.types.hsv@1</c> for a color.
/// </remarks>
/// <param name="Name">The namespaced name of the type, for example <c>core.types.hsv</c>.</param>
/// <param name="Version">The version of the type.</param>
/// <param name="DataType">The data type the definition stands for.</param>
public sealed record DataTypeDef(string Name, TypeVersion Version, DataType DataType);
