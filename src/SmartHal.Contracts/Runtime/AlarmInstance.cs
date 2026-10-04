using SmartHal.Contracts.Addressing;
using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Runtime;

/// <summary>
/// Describes a raised alarm and its lifecycle.
/// </summary>
/// <param name="Id">The immutable id of the alarm instance.</param>
/// <param name="Address">The address of the alarm; its element is the name of the alarm definition.</param>
/// <param name="Trigger">What raised the alarm.</param>
/// <param name="Severity">The severity of the alarm.</param>
/// <param name="State">The current state.</param>
/// <param name="Transitions">The states reached so far, in order.</param>
/// <param name="ShelvedUntil">The time until which the alarm is shelved; <see langword="null"/> when it is not shelved.</param>
public sealed record AlarmInstance(
    Guid Id,
    Address Address,
    AlarmTrigger Trigger,
    Severity Severity,
    AlarmState State,
    IReadOnlyList<AlarmTransition> Transitions,
    DateTimeOffset? ShelvedUntil = null);
