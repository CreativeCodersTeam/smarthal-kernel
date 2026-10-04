namespace SmartHal.Contracts.Integration.Transforms;

/// <summary>
/// Divides a constant by the value: <c>x → k / x</c>, for example to convert mired to kelvin.
/// </summary>
/// <param name="K">The constant dividend.</param>
public sealed record ReciprocalStep(double K) : TransformStep;
