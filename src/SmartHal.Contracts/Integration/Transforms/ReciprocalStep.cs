namespace SmartHal.Contracts.Integration.Transforms;

/// <summary>
/// Divides a constant by the value: <c>x → k / x</c>; the function is its own inverse.
/// </summary>
/// <remarks>
/// With <c>k = 1 000 000</c> it converts a color temperature in mired to kelvin and back.
/// </remarks>
/// <param name="K">The constant dividend.</param>
public sealed record ReciprocalStep(double K) : TransformStep;
