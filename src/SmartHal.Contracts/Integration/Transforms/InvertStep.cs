namespace SmartHal.Contracts.Integration.Transforms;

/// <summary>
/// Mirrors the value within a range: <c>x → max − x</c>; the function is its own inverse.
/// </summary>
/// <remarks>
/// Covers use it: in Matter 0 means fully open, in the core model 100 % means fully open.
/// </remarks>
/// <param name="Max">The upper end of the range.</param>
public sealed record InvertStep(double Max) : TransformStep;
