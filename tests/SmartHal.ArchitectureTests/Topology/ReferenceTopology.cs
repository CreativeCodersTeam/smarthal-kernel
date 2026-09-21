namespace SmartHal.ArchitectureTests.Topology;

/// <summary>
/// The reference topology of the specification (section 6.2): which project may reference which
/// other projects, which projects are published as packages and which stay internal.
/// </summary>
/// <param name="Allowed">Maps a project name to the projects it is allowed to reference.</param>
/// <param name="Published">The projects that are published as packages.</param>
/// <param name="Internal">The projects that stay inside the repository.</param>
internal sealed record ReferenceTopology(
    IReadOnlyDictionary<string, IReadOnlySet<string>> Allowed,
    IReadOnlySet<string> Published,
    IReadOnlySet<string> Internal)
{
    /// <summary>
    /// The topology exactly as the specification writes it down; the table is exhaustive, so every
    /// reference that is not listed here is forbidden (IF-2, FR-4).
    /// </summary>
    public static ReferenceTopology Specification { get; } = new(
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["SmartHal.Contracts"] = NamesOf(),
            ["SmartHal.Adapter.Sdk"] = NamesOf("SmartHal.Contracts"),
            ["SmartHal.Automation.Sdk"] = NamesOf("SmartHal.Contracts"),
            ["SmartHal.Core.Abstractions"] = NamesOf("SmartHal.Adapter.Sdk", "SmartHal.Contracts"),
            ["SmartHal.Core"] = NamesOf(
                "SmartHal.Core.Abstractions",
                "SmartHal.Adapter.Sdk",
                "SmartHal.Contracts"),
            ["SmartHal.Server"] = NamesOf(
                "SmartHal.Core",
                "SmartHal.Core.Abstractions",
                "SmartHal.Adapter.Sdk",
                "SmartHal.Contracts"),
            ["SmartHal.Cli"] = NamesOf("SmartHal.Contracts")
        },
        NamesOf("SmartHal.Contracts", "SmartHal.Adapter.Sdk", "SmartHal.Automation.Sdk"),
        NamesOf("SmartHal.Core.Abstractions", "SmartHal.Core", "SmartHal.Server", "SmartHal.Cli"));

    private static HashSet<string> NamesOf(params string[] projectNames)
    {
        return new HashSet<string>(projectNames, StringComparer.Ordinal);
    }
}
