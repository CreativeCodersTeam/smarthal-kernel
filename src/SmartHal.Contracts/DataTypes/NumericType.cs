namespace SmartHal.Contracts.DataTypes;

/// <summary>
/// Describes a numeric value with an optional unit and optional constraints.
/// </summary>
/// <remarks>
/// The two concrete forms are <see cref="IntegerType"/> and <see cref="NumberType"/>.
/// </remarks>
/// <param name="Unit">The UCUM unit of the value, for example <c>bar</c> or <c>Cel</c>; <see langword="null"/> for a dimensionless
/// value.</param>
/// <param name="Minimum">The smallest permitted value; <see langword="null"/> when there is no lower bound.</param>
/// <param name="Maximum">The largest permitted value; <see langword="null"/> when there is no upper bound.</param>
/// <param name="Step">The granularity of the value; <see langword="null"/> when any value is permitted.</param>
public abstract record NumericType(string? Unit, double? Minimum, double? Maximum, double? Step) : DataType;
