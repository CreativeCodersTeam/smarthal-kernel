namespace SmartHal.Contracts.Integration.Transforms;

/// <summary>
/// Mirrors the value within a range: <c>x → max − x</c>.
/// </summary>
/// <param name="Max">The upper end of the range.</param>
public sealed record InvertStep(double Max) : TransformStep;
