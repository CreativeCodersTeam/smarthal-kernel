namespace SmartHal.Contracts.DataTypes;

/// <summary>
/// Describes an integral numeric value.
/// </summary>
/// <param name="Unit">The UCUM unit of the value; <see langword="null"/> for a dimensionless value.</param>
/// <param name="Minimum">The smallest permitted value; <see langword="null"/> when there is no lower bound.</param>
/// <param name="Maximum">The largest permitted value; <see langword="null"/> when there is no upper bound.</param>
/// <param name="Step">The granularity of the value; <see langword="null"/> when any integer is permitted.</param>
public sealed record IntegerType(string? Unit = null, double? Minimum = null, double? Maximum = null, double? Step = null)
    : NumericType(Unit, Minimum, Maximum, Step);
