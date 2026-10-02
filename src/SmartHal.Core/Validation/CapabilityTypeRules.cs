using SmartHal.Contracts.Schema;
using SmartHal.Core.Abstractions.Validation;

namespace SmartHal.Core.Validation;

/// <summary>
/// Checks a capability type: its data types (R1, R2), commands (R3, R4), alarm sources (R5) and feature flags (R6).
/// </summary>
internal static class CapabilityTypeRules
{
    /// <summary>
    /// Checks a capability type.
    /// </summary>
    /// <param name="type">The capability type to check.</param>
    /// <param name="path">The path of the capability type.</param>
    /// <param name="context">The context the violations are reported to.</param>
    /// <param name="index">The catalog references are resolved in; <see langword="null"/> to skip reference checks.</param>
    public static void Check(CapabilityType type, string path, ValidationContext context, CatalogIndex? index)
    {
        var features = context.Entries(type.Features, ValidationContext.Member(path, "features"), required: false)
            .Select(entry => entry.Item)
            .ToHashSet(StringComparer.Ordinal);
        var members = new Members(ValidationContext.KeysOf(type.Properties), ValidationContext.KeysOf(type.Events), features);

        var propertiesPath = ValidationContext.Member(path, "properties");

        foreach (var (property, name) in context.Entries(type.Properties, propertiesPath, required: true))
        {
            CheckProperty(property, ValidationContext.Member(propertiesPath, name), members, context, index);
        }

        var commandsPath = ValidationContext.Member(path, "commands");

        foreach (var (command, name) in context.Entries(type.Commands, commandsPath, required: true))
        {
            CheckCommand(command, name, ValidationContext.Member(commandsPath, name), members, context, index);
        }

        var eventsPath = ValidationContext.Member(path, "events");

        foreach (var (eventDef, name) in context.Entries(type.Events, eventsPath, required: true))
        {
            if (eventDef.Payload is not null)
            {
                DataTypeRules.Check(eventDef.Payload, ValidationContext.Member(ValidationContext.Member(eventsPath, name), "payload"),
                    context, index);
            }
        }

        var alarmsPath = ValidationContext.Member(path, "alarms");

        foreach (var (alarm, name) in context.Entries(type.Alarms, alarmsPath, required: true))
        {
            CheckAlarm(alarm, ValidationContext.Member(alarmsPath, name), members, context, index);
        }
    }

    private static void CheckProperty(PropertyDef property, string path, Members members, ValidationContext context, CatalogIndex? index)
    {
        DataTypeRules.Check(property.DataType, ValidationContext.Member(path, "dataType"), context, index);
        CheckFeature(property.Feature, path, members, context);

        if (property.History is not null)
        {
            ValueRules.CheckHistoryPolicy(property.History, ValidationContext.Member(path, "history"), context);
        }
    }

    private static void CheckCommand(
        CommandDef command,
        string name,
        string path,
        Members members,
        ValidationContext context,
        CatalogIndex? index)
    {
        var parametersPath = ValidationContext.Member(path, "parameters");
        var parameters = context.Entries(command.Parameters, parametersPath, required: false);

        foreach (var (parameter, parameterName) in parameters)
        {
            DataTypeRules.Check(parameter, ValidationContext.Member(parametersPath, parameterName), context, index);
        }

        if (command.Result is not null)
        {
            DataTypeRules.Check(command.Result, ValidationContext.Member(path, "result"), context, index);
        }

        ValueRules.CheckPositive(command.Timeout, ValidationContext.Member(path, "timeout"), "timeout", context);
        CheckCompletion(command, name, path, context);
        CheckNames(command.Affects, ValidationContext.Member(path, "affects"), members.Properties, ValidationCodes.UnknownProperty,
            "property", context);
        // Parameters are optional, so a missing map declares no parameter at all; a parameter whose type is null is
        // still declared and has already been reported as a null entry.
        CheckNames(
            command.RequiredParameters,
            ValidationContext.Member(path, "requiredParameters"),
            ValidationContext.KeysOf(command.Parameters) ?? [],
            ValidationCodes.UnknownParameter,
            "parameter",
            context);
        CheckFeature(command.Feature, path, members, context);
    }

    private static void CheckCompletion(CommandDef command, string name, string path, ValidationContext context)
    {
        if (command.Completion == Completion.Result && command.Result is null)
        {
            context.Add(
                ValidationContext.Member(path, "result"),
                ValidationCodes.MissingResult,
                $"The command '{name}' completes with a result but defines no result type.");
        }

        if (command.Completion == Completion.Confirmed && (ValidationContext.IsNull(command.Affects) || command.Affects.Count == 0))
        {
            context.Add(
                ValidationContext.Member(path, "affects"),
                ValidationCodes.MissingAffects,
                $"The command '{name}' completes when a property is confirmed but names no affected property.");
        }
    }

    private static void CheckNames(
        IReadOnlyList<string>? names,
        string path,
        HashSet<string>? known,
        string code,
        string kind,
        ValidationContext context)
    {
        foreach (var (name, i) in context.Entries(names, path, required: false))
        {
            // Without the declaring map there is nothing to compare with; its absence is reported on its own.
            if (known is not null && !known.Contains(name))
            {
                context.Add(ValidationContext.Index(path, i), code, $"The {kind} '{name}' is not defined.");
            }
        }
    }

    private static void CheckAlarm(AlarmDef alarm, string path, Members members, ValidationContext context, CatalogIndex? index)
    {
        var sourcePath = ValidationContext.Member(path, "source");

        switch (alarm.Source)
        {
            case null:
                context.Add(sourcePath, ValidationCodes.NullEntry, "The alarm source is null.");
                break;
            case DeviceAlarmSource device:
                CheckName(device.Event, ValidationContext.Member(sourcePath, "event"), members.Events, ValidationCodes.UnknownEvent,
                    "event", context);
                break;
            case RuleAlarmSource rule:
                CheckName(rule.Property, ValidationContext.Member(sourcePath, "property"), members.Properties,
                    ValidationCodes.UnknownProperty, "property", context);
                break;
            default:
                break;
        }

        var parametersPath = ValidationContext.Member(path, "parameters");

        foreach (var (parameter, name) in context.Entries(alarm.Parameters, parametersPath, required: false))
        {
            DataTypeRules.Check(
                parameter.DataType,
                ValidationContext.Member(ValidationContext.Member(parametersPath, name), "dataType"),
                context,
                index);
        }
    }

    // The name is nullable on purpose: a name read from JSON can be null despite its annotation.
    private static void CheckName(string? name, string path, HashSet<string>? known, string code, string kind, ValidationContext context)
    {
        if (name is null)
        {
            context.Add(path, ValidationCodes.NullEntry, $"The {kind} the alarm refers to is null.");
        }
        else if (known is not null && !known.Contains(name))
        {
            context.Add(path, code, $"The {kind} '{name}' the alarm refers to is not defined.");
        }
    }

    private static void CheckFeature(string? feature, string path, Members members, ValidationContext context)
    {
        if (feature is not null && !members.Features.Contains(feature))
        {
            context.Add(
                ValidationContext.Member(path, "feature"),
                ValidationCodes.UnknownFeature,
                $"The feature '{feature}' is not declared by the capability type.");
        }
    }

    /// <summary>
    /// Holds the names the elements of a capability type may refer to; a set is <see langword="null"/> when its
    /// mandatory map is missing, so references into it are not judged.
    /// </summary>
    private sealed record Members(HashSet<string>? Properties, HashSet<string>? Events, HashSet<string> Features);
}
