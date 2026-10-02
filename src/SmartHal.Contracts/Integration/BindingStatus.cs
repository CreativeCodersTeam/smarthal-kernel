namespace SmartHal.Contracts.Integration;

/// <summary>
/// Describes the reachability a binding reports for its device.
/// </summary>
/// <param name="State">The reachability of the device through this binding.</param>
/// <param name="LastSeen">The time the device was last heard from through this binding; <see langword="null"/> when it never was.</param>
public sealed record BindingStatus(BindingState State, DateTimeOffset? LastSeen = null);
