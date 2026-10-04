namespace SmartHal.Contracts.Integration.Transforms;

/// <summary>
/// Multiplies the value by a factor: <c>x → x · factor</c>.
/// </summary>
/// <param name="Factor">The factor, for example <c>0.01</c> for a register in hundredths.</param>
public sealed record ScaleStep(double Factor) : TransformStep;
