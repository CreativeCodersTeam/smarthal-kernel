using SmartHal.Contracts.Schema;
using SmartHal.Contracts.Topology;
using SmartHal.Core.Abstractions.Validation;

namespace SmartHal.Core.Validation;

/// <summary>
/// Checks that a device carries the channels and capabilities its device type prescribes, with their capability type
/// and their prescribed features (R13).
/// </summary>
internal static class TemplateRules
{
    /// <summary>
    /// Checks one channel template of the device type against the channels of the device.
    /// </summary>
    /// <param name="template">The channel template of the device type.</param>
    /// <param name="deviceTypeName">The name of the device type, for the messages.</param>
    /// <param name="channels">The present channels of the device with their index.</param>
    /// <param name="context">The context the violations are reported to.</param>
    public static void CheckChannel(
        ChannelTemplate template,
        string deviceTypeName,
        IReadOnlyList<(Channel Item, int Index)> channels,
        ValidationContext context)
    {
        var match = channels.FirstOrDefault(channel => channel.Item.Key == template.Key);

        if (match.Item is null)
        {
            context.Add(
                "channels",
                ValidationCodes.TemplateMismatch,
                $"The channel '{template.Key}' prescribed by the device type '{deviceTypeName}' is missing.");

            return;
        }

        if (ValidationContext.IsNull(template.Capabilities) || ValidationContext.IsNull(match.Item.Capabilities))
        {
            return;
        }

        var channelPath = ValidationContext.Index("channels", match.Index);

        foreach (var capabilityTemplate in template.Capabilities.Where(capability => !ValidationContext.IsNull(capability)))
        {
            CheckCapability(capabilityTemplate, match.Item, channelPath, deviceTypeName, context);
        }
    }

    private static void CheckCapability(
        CapabilityTemplate template,
        Channel channel,
        string channelPath,
        string deviceTypeName,
        ValidationContext context)
    {
        var capabilitiesPath = ValidationContext.Member(channelPath, "capabilities");
        var index = FindIndex(channel.Capabilities, template.Key);

        if (index < 0)
        {
            context.Add(
                capabilitiesPath,
                ValidationCodes.TemplateMismatch,
                $"The capability '{template.Key}' prescribed by the device type '{deviceTypeName}' is missing in channel '{channel.Key}'.");

            return;
        }

        var capability = channel.Capabilities[index];
        var capabilityPath = ValidationContext.Index(capabilitiesPath, index);

        if (capability.TypeRef != template.Type)
        {
            context.Add(
                ValidationContext.Member(capabilityPath, "typeRef"),
                ValidationCodes.TemplateMismatch,
                $"The capability '{template.Key}' implements '{capability.TypeRef}', but the device type '{deviceTypeName}' prescribes " +
                    $"'{template.Type}'.");
        }

        CheckFeatures(template, capability, capabilityPath, deviceTypeName, context);
    }

    private static void CheckFeatures(
        CapabilityTemplate template,
        Capability capability,
        string capabilityPath,
        string deviceTypeName,
        ValidationContext context)
    {
        // Null feature lists are reported where they occur; they are not judged against each other here.
        if (ValidationContext.IsNull(template.Features) || ValidationContext.IsNull(capability.Features))
        {
            return;
        }

        var missing = template.Features
            .Where(feature => !ValidationContext.IsNull(feature) && !capability.Features.Contains(feature, StringComparer.Ordinal))
            .ToList();

        if (missing.Count > 0)
        {
            context.Add(
                ValidationContext.Member(capabilityPath, "features"),
                ValidationCodes.TemplateMismatch,
                $"The capability '{template.Key}' lacks the features '{string.Join("', '", missing)}' prescribed by the device type " +
                    $"'{deviceTypeName}'.");
        }
    }

    private static int FindIndex(IReadOnlyList<Capability> capabilities, string key)
    {
        for (var i = 0; i < capabilities.Count; i++)
        {
            if (!ValidationContext.IsNull(capabilities[i]) && capabilities[i].Key == key)
            {
                return i;
            }
        }

        return -1;
    }
}
