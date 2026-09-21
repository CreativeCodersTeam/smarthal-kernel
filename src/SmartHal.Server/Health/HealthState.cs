using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SmartHal.Server.Health;

/// <summary>
/// The three readiness states of the server process (FR-40).
/// </summary>
public enum HealthState
{
    /// <summary>The host runs, but at least one readiness check has not passed yet.</summary>
    Starting = 0,

    /// <summary>Every readiness check passes.</summary>
    Ready = 1,

    /// <summary>At least one readiness check no longer passes.</summary>
    NotReady = 2
}

/// <summary>
/// Turns the reported result of the readiness checks into the state transitions of FR-40.
/// </summary>
/// <remarks>
/// <para>
/// The tracker is the rule alone: it neither runs a check nor writes a log event, so the rule can be
/// stated and verified without a host. <see cref="HealthStateMonitor"/> feeds it and writes the
/// events.
/// </para>
/// <para>
/// The rule is asymmetric on purpose. Before the first passing report the process is
/// <see cref="HealthState.Starting"/>, and a failing check does not move it to
/// <see cref="HealthState.NotReady"/> - a process that has never been ready cannot stop being ready.
/// Afterwards every change between passing and failing is a transition.
/// </para>
/// <para>
/// An instance is not thread-safe; the publisher that owns it is called one report at a time.
/// </para>
/// </remarks>
public sealed class HealthStateTracker
{
    /// <summary>
    /// Gets the state the last reported result left the process in.
    /// </summary>
    /// <value><see cref="HealthState.Starting"/> until a readiness check has passed for the first time.</value>
    public HealthState Current { get; private set; } = HealthState.Starting;

    /// <summary>
    /// Applies one reported result of the readiness checks.
    /// </summary>
    /// <param name="status">The combined result of the readiness checks.</param>
    /// <returns>
    /// The new state when it differs from the previous one, otherwise <see langword="null"/>.
    /// </returns>
    public HealthState? Apply(HealthStatus status)
    {
        HealthState next;

        if (status == HealthStatus.Healthy)
        {
            next = HealthState.Ready;
        }
        else
        {
            // A process that has never been ready cannot stop being ready; it is still starting.
            next = Current == HealthState.Starting ? HealthState.Starting : HealthState.NotReady;
        }

        if (next == Current)
        {
            return null;
        }

        Current = next;

        return next;
    }
}
