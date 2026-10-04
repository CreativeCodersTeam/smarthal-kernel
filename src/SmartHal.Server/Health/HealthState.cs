using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SmartHal.Server.Health;

/// <summary>
/// Specifies the readiness state of the server process.
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
/// Derives readiness state transitions from health check results, independent of any host.
/// </summary>
/// <remarks>
/// A process that has never been ready stays <see cref="HealthState.Starting"/> on failing checks.
/// Instances are not thread-safe.
/// </remarks>
public sealed class HealthStateTracker
{
    /// <summary>
    /// Gets the current readiness state.
    /// </summary>
    /// <value>The state after the last result. The default is <see cref="HealthState.Starting"/>.</value>
    public HealthState Current { get; private set; } = HealthState.Starting;

    /// <summary>
    /// Applies a health check result and reports a resulting state change.
    /// </summary>
    /// <param name="status">The combined result of the readiness checks.</param>
    /// <returns>The new state if it changed; otherwise, <see langword="null"/>.</returns>
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
