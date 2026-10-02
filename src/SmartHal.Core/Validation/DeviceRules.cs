using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;
using SmartHal.Contracts.Topology;
using SmartHal.Core.Abstractions.Validation;

namespace SmartHal.Core.Validation;

/// <summary>
/// Checks a device on its own (R8, R9) and, with a catalog, against the types it refers to (R11 to R15).
/// </summary>
internal static class DeviceRules
{
    /// <summary>
    /// Checks a device.
    /// </summary>
    /// <param name="device">The device to check.</param>
    /// <param name="context">The context the violations are reported to.</param>
    /// <param name="index">The catalog the device is checked against; <see langword="null"/> to check it on its own.</param>
    public static void Check(Device device, ValidationContext context, CatalogIndex? index)
    {
        if (!device.Virtual && device.TypeRef is null)
        {
            context.Add("typeRef", ValidationCodes.MissingDeviceType, $"The device '{device.Key}' is not virtual but has no device type.");
        }

        var channels = context.Entries(device.Channels, "channels", required: true);
        var hasChannels = !ValidationContext.IsNull(device.Channels);

        if (hasChannels)
        {
            KeyRules.CheckChannels(channels, channel => channel.Key, "channels", context);
        }

        foreach (var (channel, i) in channels)
        {
            CheckChannel(channel, ValidationContext.Index("channels", i), context, index);
        }

        if (index is not null && device.TypeRef is { } typeRef)
        {
            // Without the channel list only the reference itself can be resolved; the template is not judged.
            CheckAgainstDeviceType(typeRef, hasChannels ? channels : null, context, index);
        }
    }

    private static void CheckChannel(Channel channel, string path, ValidationContext context, CatalogIndex? index)
    {
        var capabilitiesPath = ValidationContext.Member(path, "capabilities");
        var capabilities = context.Entries(channel.Capabilities, capabilitiesPath, required: true);

        KeyRules.CheckUnique(capabilities, capability => capability.Key, capabilitiesPath, "capability", context);

        foreach (var (capability, i) in capabilities)
        {
            CheckCapability(capability, ValidationContext.Index(capabilitiesPath, i), context, index);
        }

        if (index is null || channel.Profile is not { } profileRef)
        {
            return;
        }

        var profile = index.FindProfile(profileRef);

        if (profile is null)
        {
            SchemaRules.ReportUnresolved(profileRef, "channel profile", ValidationContext.Member(path, "profile"), context);
        }
        else if (!ValidationContext.IsNull(channel.Capabilities))
        {
            // Without the capability list the profile cannot be judged; its absence is reported on its own.
            ProfileRules.Check(profile, [.. capabilities.Select(entry => entry.Item.TypeRef)], path, context);
        }
    }

    private static void CheckCapability(Capability capability, string path, ValidationContext context, CatalogIndex? index)
    {
        var features = context.Entries(capability.Features, ValidationContext.Member(path, "features"), required: true);
        var historyOverridesPath = ValidationContext.Member(path, "historyOverrides");
        var historyOverrides = context.Entries(capability.HistoryOverrides, historyOverridesPath, required: false);

        foreach (var (policy, property) in historyOverrides)
        {
            ValueRules.CheckHistoryPolicy(policy, ValidationContext.Member(historyOverridesPath, property), context);
        }

        var alarmParameters = context.Entries(capability.AlarmParameters, ValidationContext.Member(path, "alarmParameters"),
            required: false);

        if (index is null)
        {
            return;
        }

        var type = index.FindCapability(capability.TypeRef);

        if (type is null)
        {
            SchemaRules.ReportUnresolved(capability.TypeRef, "capability type", ValidationContext.Member(path, "typeRef"), context);

            return;
        }

        CheckVersion(capability, type, path, context);
        CheckFeatures(features, type, path, context);
        CapabilityOverrideRules.CheckHistoryOverrides(historyOverrides, type, path, context);
        CapabilityOverrideRules.CheckAlarmParameters(alarmParameters, type, path, context);
    }

    private static void CheckVersion(Capability capability, CapabilityType type, string path, ValidationContext context)
    {
        var versionPath = ValidationContext.Member(path, "version");

        if (capability.Version.Major != capability.TypeRef.Major)
        {
            context.Add(
                versionPath,
                ValidationCodes.VersionMismatch,
                ValidationContext.Invariant($"The capability implements version {capability.Version} but refers to ")
                    + ValidationContext.Invariant($"'{capability.TypeRef}'."));
        }
        else if (capability.Version.Minor > type.Version.Minor)
        {
            context.Add(
                versionPath,
                ValidationCodes.VersionMismatch,
                ValidationContext.Invariant($"The capability implements version {capability.Version}, which is newer than version ")
                    + ValidationContext.Invariant($"{type.Version} in the type catalog."));
        }
    }

    private static void CheckFeatures(IReadOnlyList<(string Item, int Index)> features, CapabilityType type, string path,
        ValidationContext context)
    {
        var declared = ValidationContext.IsNull(type.Features) ? [] : type.Features.ToHashSet(StringComparer.Ordinal);
        var featuresPath = ValidationContext.Member(path, "features");

        foreach (var (feature, i) in features)
        {
            if (!declared.Contains(feature))
            {
                context.Add(
                    ValidationContext.Index(featuresPath, i),
                    ValidationCodes.UnknownFeature,
                    $"The feature '{feature}' is not declared by the capability type '{type.Name}'.");
            }
        }
    }

    private static void CheckAgainstDeviceType(
        TypeRef typeRef,
        IReadOnlyList<(Channel Item, int Index)>? channels,
        ValidationContext context,
        CatalogIndex index)
    {
        var deviceType = index.FindDeviceType(typeRef);

        if (deviceType is null)
        {
            SchemaRules.ReportUnresolved(typeRef, "device type", "typeRef", context);

            return;
        }

        if (channels is null || ValidationContext.IsNull(deviceType.Channels))
        {
            return;
        }

        foreach (var template in deviceType.Channels.Where(template => !ValidationContext.IsNull(template)))
        {
            TemplateRules.CheckChannel(template, deviceType.Name, channels, context);
        }
    }
}
