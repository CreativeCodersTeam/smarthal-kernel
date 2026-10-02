namespace SmartHal.Contracts.DataTypes;

/// <summary>
/// Describes a list of values of one data type.
/// </summary>
/// <param name="Items">The data type of every element.</param>
/// <param name="MaxItems">The largest permitted number of elements; <see langword="null"/> when the length is not limited.</param>
public sealed record ArrayType(DataType Items, int? MaxItems = null) : DataType;
