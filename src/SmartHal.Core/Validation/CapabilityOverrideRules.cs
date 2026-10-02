using System.Text.Json.Nodes;
using SmartHal.Contracts.Schema;
using SmartHal.Core.Abstractions.Validation;

namespace SmartHal.Core.Validation;

/// <summary>
/// Checks the instance overrides of a capability against its capability type (R15).
/// </summary>
internal static class CapabilityOverrideRules
{
    /// <summary>
    /// Checks that every history override names a property of the capability type.
    /// </summary>
    /// <param name="overrides">The present history overrides with their property name.</param>
    /// <param name="type">The capability type.</param>
    /// <param name="path">The path of the capability.</param>
    /// <param name="context">The context the violations are reported to.</param>
    public static void CheckHistoryOverrides(
        IReadOnlyList<(HistoryPolicy Item, string Key)> overrides,
        CapabilityType type,
        string path,
        ValidationContext context)
    {
        var overridesPath = ValidationContext.Member(path, "historyOverrides");

        // A capability type without its property map is invalid on its own; overrides are not judged against it.
        if (ValidationContext.IsNull(type.Properties))
        {
            return;
        }

        foreach (var (_, property) in overrides)
        {
            if (!type.Properties.ContainsKey(property))
            {
                context.Add(
                    ValidationContext.Member(overridesPath, property),
                    ValidationCodes.UnknownProperty,
                    $"The history override names the property '{property}', which the capability type '{type.Name}' does not define.");
            }
        }
    }

    /// <summary>
    /// Checks that every alarm parameter value names an alarm of the capability type and one of its parameters.
    /// </summary>
    /// <param name="alarmParameters">The present parameter values per alarm name.</param>
    /// <param name="type">The capability type.</param>
    /// <param name="path">The path of the capability.</param>
    /// <param name="context">The context the violations are reported to.</param>
    public static void CheckAlarmParameters(
        IReadOnlyList<(IReadOnlyDictionary<string, JsonNode?> Item, string Key)> alarmParameters,
        CapabilityType type,
        string path,
        ValidationContext context)
    {
        var parametersPath = ValidationContext.Member(path, "alarmParameters");

        // A capability type without its alarm map is invalid on its own; overrides are not judged against it.
        if (ValidationContext.IsNull(type.Alarms))
        {
            return;
        }

        foreach (var (values, alarmName) in alarmParameters)
        {
            var alarmPath = ValidationContext.Member(parametersPath, alarmName);

            if (!type.Alarms.TryGetValue(alarmName, out var alarm))
            {
                context.Add(
                    alarmPath,
                    ValidationCodes.UnknownAlarm,
                    $"The alarm '{alarmName}' is not defined by the capability type '{type.Name}'.");

                continue;
            }

            // The alarm is declared, but its definition is null; that is the type's own problem, so its parameters
            // cannot be judged here.
            if (ValidationContext.IsNull(alarm))
            {
                continue;
            }

            var known = alarm.Parameters;

            foreach (var parameter in values.Keys.Where(parameter => ValidationContext.IsNull(known) || !known.ContainsKey(parameter)))
            {
                context.Add(
                    ValidationContext.Member(alarmPath, parameter),
                    ValidationCodes.UnknownAlarmParameter,
                    $"The alarm '{alarmName}' defines no parameter '{parameter}'.");
            }
        }
    }
}
