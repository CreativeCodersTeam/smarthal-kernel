namespace SmartHal.Contracts.Schema;

/// <summary>
/// Defines the minimum change a value needs before it is stored, so that noisy sensors do not flood the history.
/// </summary>
/// <param name="Absolute">The minimum absolute change in the unit of the property; <see langword="null"/> when not limited.</param>
/// <param name="Relative">The minimum relative change as a fraction of the last stored value; <see langword="null"/> when not
/// limited.</param>
/// <param name="MinInterval">The minimum time between two stored values; <see langword="null"/> when not limited.</param>
public sealed record Deadband(double? Absolute = null, double? Relative = null, TimeSpan? MinInterval = null);
