namespace SmartHal.Contracts.Integration.Transforms;

/// <summary>
/// Adds a constant to the value: <c>x → x + value</c>.
/// </summary>
/// <param name="Value">The constant to add.</param>
public sealed record OffsetStep(double Value) : TransformStep;
