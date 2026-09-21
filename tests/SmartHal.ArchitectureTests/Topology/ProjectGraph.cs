using System.Xml.Linq;

namespace SmartHal.ArchitectureTests.Topology;

/// <summary>
/// The project reference graph of the production projects, read from the project files rather than
/// from the compiled assemblies: an unused project reference leaves no trace in an assembly (C-5).
/// </summary>
/// <param name="References">
/// Maps a project name to the names of the projects it references. Every name is the bare project
/// name without a path and without the file extension.
/// </param>
internal sealed record ProjectGraph(IReadOnlyDictionary<string, IReadOnlySet<string>> References)
{
    private const string SourceFolderName = "src";

    /// <summary>
    /// Reads the graph from the project files below <c>src/</c>.
    /// </summary>
    /// <param name="repositoryRoot">The absolute path of the repository root.</param>
    /// <returns>The reference graph of every production project.</returns>
    /// <exception cref="DirectoryNotFoundException">The repository has no <c>src/</c> folder.</exception>
    public static ProjectGraph ReadFromSource(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var sourceRoot = Path.Combine(repositoryRoot, SourceFolderName);

        if (!Directory.Exists(sourceRoot))
        {
            throw new DirectoryNotFoundException($"'{sourceRoot}' does not exist.");
        }

        var references = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

        foreach (var projectFile in Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories))
        {
            references[Path.GetFileNameWithoutExtension(projectFile)] = ReadReferencesOf(projectFile);
        }

        return new ProjectGraph(references);
    }

    private static HashSet<string> ReadReferencesOf(string projectFile)
    {
        return XDocument
            .Load(projectFile)
            .Descendants("ProjectReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', '/')))
            .ToHashSet(StringComparer.Ordinal);
    }
}
