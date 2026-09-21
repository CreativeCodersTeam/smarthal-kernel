namespace SmartHal.ArchitectureTests.Topology;

/// <summary>
/// One project reference that a topology rule rejects.
/// </summary>
/// <param name="From">The referencing project.</param>
/// <param name="To">The referenced project.</param>
/// <param name="Rule">The rule the reference breaks.</param>
internal sealed record TopologyViolation(string From, string To, string Rule)
{
    /// <summary>
    /// Returns the message that names the offending reference (AC-18).
    /// </summary>
    public override string ToString() => $"{From} → {To} violates {Rule}";
}

/// <summary>
/// The reference topology rules. They are pure functions over a <see cref="ProjectGraph"/> and a
/// <see cref="ReferenceTopology"/>, so the failure case can be proven with a synthetic graph
/// instead of a deliberately broken repository (C-5).
/// </summary>
internal static class TopologyRules
{
    private const string AllowedReferencesRule = "the reference table of section 6.2 (FR-4, FR-55)";

    private const string PublishedToInternalRule =
        "the ban on a published project referencing an internal one (FR-9, FR-56)";

    private const string SdkCrossReferenceRule =
        "the ban on references between the two SDK projects (FR-8, FR-57)";

    // The two SDK projects are the published projects that carry the SDK suffix; deriving them
    // keeps the rule data-driven instead of repeating the project names here.
    private const string SdkProjectSuffix = ".Sdk";

    /// <summary>
    /// Returns every reference that the topology table does not list.
    /// </summary>
    public static IReadOnlyList<TopologyViolation> FindForbiddenReferences(
        ProjectGraph graph,
        ReferenceTopology topology)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(topology);

        return ReferencesOf(graph)
            .Where(reference => !IsAllowed(topology, reference.From, reference.To))
            .Select(reference => new TopologyViolation(reference.From, reference.To, AllowedReferencesRule))
            .ToArray();
    }

    /// <summary>
    /// Returns every reference that leads from a published project to an internal one.
    /// </summary>
    public static IReadOnlyList<TopologyViolation> FindPublishedToInternal(
        ProjectGraph graph,
        ReferenceTopology topology)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(topology);

        return ReferencesOf(graph)
            .Where(reference => topology.Published.Contains(reference.From)
                && topology.Internal.Contains(reference.To))
            .Select(reference => new TopologyViolation(reference.From, reference.To, PublishedToInternalRule))
            .ToArray();
    }

    /// <summary>
    /// Returns every reference between the two SDK projects.
    /// </summary>
    public static IReadOnlyList<TopologyViolation> FindSdkCrossReferences(
        ProjectGraph graph,
        ReferenceTopology topology)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(topology);

        var sdkProjects = topology.Published
            .Where(project => project.EndsWith(SdkProjectSuffix, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        return ReferencesOf(graph)
            .Where(reference => sdkProjects.Contains(reference.From) && sdkProjects.Contains(reference.To))
            .Select(reference => new TopologyViolation(reference.From, reference.To, SdkCrossReferenceRule))
            .ToArray();
    }

    private static bool IsAllowed(ReferenceTopology topology, string from, string to)
    {
        return topology.Allowed.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }

    private static IEnumerable<(string From, string To)> ReferencesOf(ProjectGraph graph)
    {
        return graph.References
            .OrderBy(project => project.Key, StringComparer.Ordinal)
            .SelectMany(project => project.Value
                .OrderBy(reference => reference, StringComparer.Ordinal)
                .Select(reference => (From: project.Key, To: reference)));
    }
}
