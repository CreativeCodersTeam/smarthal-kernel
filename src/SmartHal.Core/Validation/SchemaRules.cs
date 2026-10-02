using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;
using SmartHal.Core.Abstractions.Validation;

namespace SmartHal.Core.Validation;

/// <summary>
/// Checks the schema types besides capability types: reusable data types, channel profiles (R7), device types (R8)
/// and migrations (R10), and with a catalog their type references (R11) and the profiles of channel templates (R14).
/// </summary>
internal static class SchemaRules
{
    /// <summary>
    /// Checks a reusable data type definition.
    /// </summary>
    /// <remarks>
    /// A definition without a name cannot be referenced; it is reported as a <see langword="null"/> entry at its name.
    /// Reading through the contract serializer never produces one, because the name is mandatory there.
    /// </remarks>
    /// <param name="definition">The definition to check.</param>
    /// <param name="path">The path of the definition.</param>
    /// <param name="context">The context the violations are reported to.</param>
    /// <param name="index">The catalog references are resolved in; <see langword="null"/> to skip reference checks.</param>
    public static void Check(DataTypeDef definition, string path, ValidationContext context, CatalogIndex? index)
    {
        if (ValidationContext.IsNull(definition.Name))
        {
            context.Add(ValidationContext.Member(path, "name"), ValidationCodes.NullEntry, "The data type definition has no name.");
        }

        DataTypeRules.CheckDefinition(definition, ValidationContext.Member(path, "dataType"), context, index);
    }

    /// <summary>
    /// Checks a channel profile.
    /// </summary>
    /// <param name="profile">The profile to check.</param>
    /// <param name="path">The path of the profile.</param>
    /// <param name="context">The context the violations are reported to.</param>
    /// <param name="index">The catalog references are resolved in; <see langword="null"/> to skip reference checks.</param>
    public static void Check(ChannelProfile profile, string path, ValidationContext context, CatalogIndex? index)
    {
        var capabilitiesPath = ValidationContext.Member(path, "capabilities");

        foreach (var (capability, i) in context.Entries(profile.Capabilities, capabilitiesPath, required: true))
        {
            var capabilityPath = ValidationContext.Index(capabilitiesPath, i);

            CheckCounts(capability, capabilityPath, context);

            if (index is not null && index.IsUnresolvedCapability(capability.Type))
            {
                ReportUnresolved(capability.Type, "capability type", ValidationContext.Member(capabilityPath, "type"), context);
            }
        }
    }

    /// <summary>
    /// Checks a device type.
    /// </summary>
    /// <param name="deviceType">The device type to check.</param>
    /// <param name="path">The path of the device type.</param>
    /// <param name="context">The context the violations are reported to.</param>
    /// <param name="index">The catalog references are resolved in; <see langword="null"/> to skip reference checks.</param>
    public static void Check(DeviceType deviceType, string path, ValidationContext context, CatalogIndex? index)
    {
        var channelsPath = ValidationContext.Member(path, "channels");
        var channels = context.Entries(deviceType.Channels, channelsPath, required: true);

        if (!ValidationContext.IsNull(deviceType.Channels))
        {
            KeyRules.CheckChannels(channels, channel => channel.Key, channelsPath, context);
        }

        foreach (var (channel, i) in channels)
        {
            CheckChannelTemplate(channel, ValidationContext.Index(channelsPath, i), context, index);
        }

        if (deviceType.Sleepy is not null)
        {
            ValueRules.CheckPositive(
                deviceType.Sleepy.WakeInterval,
                ValidationContext.Member(ValidationContext.Member(path, "sleepy"), "wakeInterval"),
                "wake-up interval",
                context);
        }

        var templatesPath = ValidationContext.Member(path, "bindingTemplates");

        foreach (var (template, i) in context.Entries(deviceType.BindingTemplates, templatesPath, required: false))
        {
            var templatePath = ValidationContext.Index(templatesPath, i);
            var parametersPath = ValidationContext.Member(templatePath, "parameters");

            foreach (var (parameter, name) in context.Entries(template.Parameters, parametersPath, required: true))
            {
                DataTypeRules.Check(parameter, ValidationContext.Member(parametersPath, name), context, index);
            }

            var mappingsPath = ValidationContext.Member(templatePath, "mappings");

            foreach (var (mapping, j) in context.Entries(template.Mappings, mappingsPath, required: true))
            {
                if (mapping.PollInterval is { } pollInterval)
                {
                    ValueRules.CheckPositive(
                        pollInterval,
                        ValidationContext.Member(ValidationContext.Index(mappingsPath, j), "pollInterval"),
                        "poll interval",
                        context);
                }
            }
        }
    }

    /// <summary>
    /// Checks a capability migration.
    /// </summary>
    /// <param name="migration">The migration to check.</param>
    /// <param name="path">The path of the migration.</param>
    /// <param name="context">The context the violations are reported to.</param>
    /// <param name="index">The catalog references are resolved in; <see langword="null"/> to skip reference checks.</param>
    public static void Check(CapabilityMigration migration, string path, ValidationContext context, CatalogIndex? index)
    {
        // A reference without a name (for example a default TypeRef read from incomplete JSON) cannot be compared or
        // resolved; it is reported once and the migration rules that need it are skipped.
        var missing = false;

        foreach (var (reference, member) in new[] { (migration.From, "from"), (migration.To, "to") })
        {
            if (ValidationContext.IsNull(reference.Name))
            {
                context.Add(ValidationContext.Member(path, member), ValidationCodes.NullEntry, $"The '{member}' reference has no name.");
                missing = true;
            }
        }

        if (missing)
        {
            return;
        }

        if (!string.Equals(migration.From.Name, migration.To.Name, StringComparison.Ordinal))
        {
            context.Add(
                path,
                ValidationCodes.InvalidMigration,
                $"The migration leads from '{migration.From}' to the different type '{migration.To}'.");
        }

        if (migration.To.Major <= migration.From.Major)
        {
            context.Add(
                path,
                ValidationCodes.InvalidMigration,
                $"The migration from '{migration.From}' does not lead to a higher major version but to '{migration.To}'.");
        }

        if (index is null)
        {
            return;
        }

        foreach (var (reference, member) in new[] { (migration.From, "from"), (migration.To, "to") })
        {
            if (index.IsUnresolvedCapability(reference))
            {
                ReportUnresolved(reference, "capability type", ValidationContext.Member(path, member), context);
            }
        }
    }

    /// <summary>
    /// Reports a type reference that cannot be resolved in the catalog.
    /// </summary>
    /// <param name="reference">The reference.</param>
    /// <param name="kind">The kind of type the reference points to, for example <c>capability type</c>.</param>
    /// <param name="path">The path of the reference.</param>
    /// <param name="context">The context the violation is reported to.</param>
    public static void ReportUnresolved(TypeRef reference, string kind, string path, ValidationContext context) =>
        context.Add(path, ValidationCodes.UnresolvedType, $"The {kind} '{reference}' is not in the type catalog.");

    private static void CheckChannelTemplate(ChannelTemplate channel, string path, ValidationContext context, CatalogIndex? index)
    {
        var capabilitiesPath = ValidationContext.Member(path, "capabilities");
        var capabilities = context.Entries(channel.Capabilities, capabilitiesPath, required: true);

        KeyRules.CheckUnique(capabilities, capability => capability.Key, capabilitiesPath, "capability", context);

        if (index is null)
        {
            return;
        }

        foreach (var (capability, i) in capabilities)
        {
            if (index.IsUnresolvedCapability(capability.Type))
            {
                ReportUnresolved(
                    capability.Type,
                    "capability type",
                    ValidationContext.Member(ValidationContext.Index(capabilitiesPath, i), "type"),
                    context);
            }
        }

        if (channel.Profile is not { } profileRef)
        {
            return;
        }

        var profile = index.FindProfile(profileRef);

        if (profile is null)
        {
            if (!index.ProfilesUnknown)
            {
                ReportUnresolved(profileRef, "channel profile", ValidationContext.Member(path, "profile"), context);
            }
        }
        else if (!ValidationContext.IsNull(channel.Capabilities))
        {
            // Without the capability list the profile cannot be judged; its absence is reported on its own.
            ProfileRules.Check(profile, [.. capabilities.Select(entry => entry.Item.Type)], path, context);
        }
    }

    private static void CheckCounts(ProfileCapability capability, string path, ValidationContext context)
    {
        foreach (var (count, member) in new[] { (capability.Min, "min"), (capability.Max, "max") })
        {
            if (count < 0)
            {
                context.Add(ValidationContext.Member(path, member), ValidationCodes.InvalidRange,
                    ValidationContext.Invariant($"The {member} count {count} is negative."));
            }
        }

        if (capability is { Min: >= 0 and var min, Max: >= 0 and var max } && min > max)
        {
            context.Add(
                ValidationContext.Member(path, "min"),
                ValidationCodes.InvalidRange,
                ValidationContext.Invariant($"The minimum count {min} is greater than the maximum count {max}."));
        }
    }
}
