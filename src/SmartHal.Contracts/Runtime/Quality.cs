namespace SmartHal.Contracts.Runtime;

/// <summary>
/// Describes the quality of a value and why it is not good.
/// </summary>
/// <param name="Level">How trustworthy the value is.</param>
/// <param name="Reason">Why the value is not good, see <see cref="QualityReasons"/>; <see langword="null"/> for a good value.</param>
public sealed record Quality(QualityLevel Level, string? Reason = null);
