using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Api;

/// <summary>
/// Selects devices, states, alarms or messages; every operation of the API uses the same filter.
/// </summary>
/// <remarks>
/// <para>
/// Every criterion that is set narrows the selection; a filter without criteria selects everything.
/// </para>
/// <para>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </para>
/// </remarks>
/// <param name="LocationId">The location whose subtree is selected; <see langword="null"/> for every location.</param>
/// <param name="KeyPattern">A key pattern such as <c>halle2.*</c>; <see langword="null"/> for every key.</param>
/// <param name="CapabilityTypes">The capability types to select; <see langword="null"/> for every type.</param>
/// <param name="Tags">The tags an object must carry; <see langword="null"/> when tags do not matter.</param>
/// <param name="Kinds">The message kinds to select: <c>StateChanged</c>, <c>EventOccurred</c> or <c>CommandUpdated</c>; <see
/// langword="null"/> for every kind.</param>
public sealed record Filter(
    Guid? LocationId = null,
    string? KeyPattern = null,
    IReadOnlyList<TypeRef>? CapabilityTypes = null,
    IReadOnlyDictionary<string, string>? Tags = null,
    IReadOnlyList<string>? Kinds = null);
