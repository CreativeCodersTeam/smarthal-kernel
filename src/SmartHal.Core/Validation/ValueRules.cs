using SmartHal.Contracts.Schema;
using SmartHal.Core.Abstractions.Validation;

namespace SmartHal.Core.Validation;

/// <summary>
/// Checks values that only make sense in one direction: timeouts, intervals and retentions that have to be positive,
/// and history policies with their rollups and deadband.
/// </summary>
internal static class ValueRules
{
    /// <summary>
    /// Reports a span that is not greater than zero.
    /// </summary>
    /// <param name="value">The span to check.</param>
    /// <param name="path">The path of the span.</param>
    /// <param name="description">What the span is, for example <c>timeout</c>.</param>
    /// <param name="context">The context the violation is reported to.</param>
    public static void CheckPositive(TimeSpan value, string path, string description, ValidationContext context)
    {
        if (value <= TimeSpan.Zero)
        {
            context.Add(
                path,
                ValidationCodes.InvalidDuration,
                ValidationContext.Invariant($"The {description} {value:c} is not greater than zero."));
        }
    }

    /// <summary>
    /// Checks a history policy: a positive raw retention, positive rollup intervals and retentions, and a deadband
    /// without negative values.
    /// </summary>
    /// <param name="policy">The policy to check.</param>
    /// <param name="path">The path of the policy.</param>
    /// <param name="context">The context the violations are reported to.</param>
    public static void CheckHistoryPolicy(HistoryPolicy policy, string path, ValidationContext context)
    {
        CheckPositive(policy.RawRetention, ValidationContext.Member(path, "rawRetention"), "raw retention", context);

        var rollupsPath = ValidationContext.Member(path, "rollups");

        foreach (var (rollup, i) in context.Entries(policy.Rollups, rollupsPath, required: false))
        {
            var rollupPath = ValidationContext.Index(rollupsPath, i);

            CheckPositive(rollup.Interval, ValidationContext.Member(rollupPath, "interval"), "rollup interval", context);
            CheckPositive(rollup.Retention, ValidationContext.Member(rollupPath, "retention"), "rollup retention", context);

            if (ValidationContext.IsNull(rollup.Aggregates))
            {
                context.Add(ValidationContext.Member(rollupPath, "aggregates"), ValidationCodes.NullEntry,
                    "The mandatory collection is null.");
            }
        }

        if (policy.Deadband is { } deadband)
        {
            CheckDeadband(deadband, ValidationContext.Member(path, "deadband"), context);
        }
    }

    private static void CheckDeadband(Deadband deadband, string path, ValidationContext context)
    {
        foreach (var (value, member) in new[] { (deadband.Absolute, "absolute"), (deadband.Relative, "relative") })
        {
            if (value is { } change && (!double.IsFinite(change) || change < 0))
            {
                context.Add(
                    ValidationContext.Member(path, member),
                    ValidationCodes.InvalidRange,
                    ValidationContext.Invariant($"The {member} deadband {change} is not a finite number of zero or more."));
            }
        }

        if (deadband.MinInterval is { } minInterval && minInterval < TimeSpan.Zero)
        {
            context.Add(
                ValidationContext.Member(path, "minInterval"),
                ValidationCodes.InvalidDuration,
                ValidationContext.Invariant($"The minimum interval {minInterval:c} is negative."));
        }
    }
}
