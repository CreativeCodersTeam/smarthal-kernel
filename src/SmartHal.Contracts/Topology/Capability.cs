using System.Text.Json.Nodes;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;

namespace SmartHal.Contracts.Topology;

/// <summary>
/// Describes a capability instance: a capability type implemented on a channel, with its active features.
/// </summary>
/// <param name="Id">The immutable id of the capability; it survives a major version change of its type.</param>
/// <param name="Key">The key of the capability, unique within the channel, for example <c>druckseite</c>.</param>
/// <param name="TypeRef">The implemented capability type.</param>
/// <param name="Version">The version of the type actually implemented, including its minor version.</param>
/// <param name="Features">The active feature flags.</param>
/// <param name="BindingId">The id of the binding that is the source of the capability.</param>
/// <param name="HistoryOverrides">The history policies that override the property defaults, keyed by property name; <see langword="null"/>
/// when none do.</param>
/// <param name="AlarmParameters">The concrete alarm parameter values, keyed by alarm name and then by parameter name; <see
/// langword="null"/> when every default applies.</param>
/// <param name="Aliases">The former keys, valid for a limited time; <see langword="null"/> when there are none.</param>
/// <param name="Tags">Free key/value tags the kernel does not check; <see langword="null"/> when there are none.</param>
public sealed record Capability(
    Guid Id,
    string Key,
    TypeRef TypeRef,
    TypeVersion Version,
    IReadOnlyList<string> Features,
    Guid BindingId,
    IReadOnlyDictionary<string, HistoryPolicy>? HistoryOverrides = null,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, JsonNode?>>? AlarmParameters = null,
    IReadOnlyList<string>? Aliases = null,
    IReadOnlyDictionary<string, string>? Tags = null) : IIdentified;
