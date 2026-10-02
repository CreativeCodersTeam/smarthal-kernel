namespace SmartHal.Contracts.DataTypes;

/// <summary>
/// Describes a text value.
/// </summary>
/// <param name="MaxLength">The largest permitted number of characters; <see langword="null"/> when the length is not limited.</param>
/// <param name="Pattern">A regular expression the value has to match; <see langword="null"/> when any text is permitted.</param>
public sealed record StringType(int? MaxLength = null, string? Pattern = null) : DataType;
