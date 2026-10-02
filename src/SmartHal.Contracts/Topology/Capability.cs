using System.Text.Json.Nodes;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;

namespace SmartHal.Contracts.Topology;

/// <summary>
/// Describes a capability instance: a capability type implemented on a channel, with its active features.
/// </summary>
/// <remarks>
/// <para>
/// All runtime values - state, commands, events and alarms - hang off the capability. A channel may carry the same
/// capability type several times, distinguished by <see cref="Key"/>, for example <c>core.pressure</c> as
/// <c>saugseite</c> and <c>druckseite</c>.
/// </para>
/// <para>
/// Every capability has exactly one binding as its source; different capabilities of a device may use different
/// bindings.
/// </para>
/// <para>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </para>
/// </remarks>
/// <param name="Id">The immutable id of the capability; it survives a major version change of its type.</param>
/// <param name="Key">The key of the capability, unique within the channel, for example <c>druckseite</c>.</param>
/// <param name="TypeRef">The implemented capability type.</param>
/// <param name="Version">The version of the type actually implemented, including its minor version.</param>
/// <param name="Features">The active feature flags.</param>
/// <param name="BindingId">The id of the binding that is the source of the capability.</param>
/// <param name="HistoryOverrides">The history policy per property that overrides the default of the property definition; <see
/// langword="null"/> when every default applies.</param>
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
