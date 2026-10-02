namespace SmartHal.Contracts.Runtime;

/// <summary>
/// Records when an alarm instance reached a state and who caused it.
/// </summary>
/// <param name="State">The state reached.</param>
/// <param name="At">The time the state was reached.</param>
/// <param name="By">Who caused the transition, for example the user who acknowledged; <see langword="null"/> for an automatic
/// transition.</param>
public sealed record AlarmTransition(AlarmState State, DateTimeOffset At, string? By = null);
