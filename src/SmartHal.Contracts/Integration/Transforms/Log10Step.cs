namespace SmartHal.Contracts.Integration.Transforms;

/// <summary>
/// Takes the decadic logarithm of the value: <c>x → factor · log10(x) + offset</c>; the inverse is
/// <c>y → 10^((y − offset) / factor)</c>.
/// </summary>
/// <param name="Factor">The factor applied to the logarithm; <see langword="null"/> means 1.</param>
/// <param name="Offset">The constant added to the result; <see langword="null"/> means 0.</param>
public sealed record Log10Step(double? Factor = null, double? Offset = null) : TransformStep;
