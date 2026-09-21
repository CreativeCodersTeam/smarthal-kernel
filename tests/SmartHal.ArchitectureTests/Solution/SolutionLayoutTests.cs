using System.Xml.Linq;
using AwesomeAssertions;
using Xunit;

namespace SmartHal.ArchitectureTests.Solution;

/// <summary>
/// Verifies the repository layout and the contents of the solution file against the specification
/// (IF-1, FR-1, FR-2, FR-3, AC-22).
/// </summary>
public sealed class SolutionLayoutTests
{
    private static readonly string[] ProductionProjectNames =
    [
        "SmartHal.Contracts",
        "SmartHal.Adapter.Sdk",
        "SmartHal.Automation.Sdk",
        "SmartHal.Core.Abstractions",
        "SmartHal.Core",
        "SmartHal.Server",
        "SmartHal.Cli"
    ];

    private static readonly string[] TestProjectNames =
    [
        "SmartHal.Contracts.UnitTests",
        "SmartHal.Adapter.Sdk.UnitTests",
        "SmartHal.Automation.Sdk.UnitTests",
        "SmartHal.Core.Abstractions.UnitTests",
        "SmartHal.Core.UnitTests",
        "SmartHal.Server.UnitTests",
        "SmartHal.Cli.UnitTests",
        "SmartHal.IntegrationTests",
        "SmartHal.ArchitectureTests"
    ];

    private static readonly string[] RequiredRootFiles =
    [
        "SmartHal.slnx",
        "global.json",
        "Directory.Build.props",
        "Directory.Packages.props",
        ".editorconfig"
    ];

    private static readonly string[] RequiredRootDirectories = ["src", "tests"];

    [Fact]
    public void Solution_Built_ContainsSevenProductionAndNineTestProjects()
    {
        // Arrange
        var root = RepositoryLocator.FindRoot();

        // Act
        var productionProjects = ProjectNamesUnder(Path.Combine(root, "src"));
        var testProjects = ProjectNamesUnder(Path.Combine(root, "tests"));

        // Assert
        productionProjects.Should().BeEquivalentTo(ProductionProjectNames).And.HaveCount(7);
        testProjects.Should().BeEquivalentTo(TestProjectNames).And.HaveCount(9);
    }

    [Fact]
    public void RepositoryLayout_Inspected_MatchesSpecifiedTree()
    {
        // Arrange
        var root = RepositoryLocator.FindRoot();

        // Act
        var missingFiles = RequiredRootFiles
            .Where(file => !File.Exists(Path.Combine(root, file)))
            .ToArray();
        var missingDirectories = RequiredRootDirectories
            .Where(directory => !Directory.Exists(Path.Combine(root, directory)))
            .ToArray();

        // Assert
        missingFiles.Should().BeEmpty("the repository root must contain the files listed in IF-1");
        missingDirectories.Should().BeEmpty("the repository root must contain 'src/' and 'tests/'");
    }

    [Fact]
    public void SolutionFile_Parsed_ListsOnlyProjectsUnderSrcAndTests()
    {
        // Arrange & Act
        var projectPaths = SolutionProjectPaths();

        // Assert
        projectPaths.Should().NotBeEmpty();
        projectPaths.Should().OnlyContain(path =>
            path.StartsWith("src/", StringComparison.Ordinal) ||
            path.StartsWith("tests/", StringComparison.Ordinal));
    }

    [Fact]
    public void SolutionFile_Parsed_ListsExactlyTheSevenProductionProjects()
    {
        // Arrange & Act
        var productionProjects = SolutionProjectNamesUnder("src/");

        // Assert
        productionProjects.Should().BeEquivalentTo(ProductionProjectNames);
    }

    [Fact]
    public void SolutionFile_Parsed_ListsExactlyTheNineTestProjects()
    {
        // Arrange & Act
        var testProjects = SolutionProjectNamesUnder("tests/");

        // Assert
        testProjects.Should().BeEquivalentTo(TestProjectNames);
    }

    private static string[] ProjectNamesUnder(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .ToArray();
    }

    private static string[] SolutionProjectNamesUnder(string prefix)
    {
        return SolutionProjectPaths()
            .Where(path => path.StartsWith(prefix, StringComparison.Ordinal))
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .ToArray();
    }

    private static string[] SolutionProjectPaths()
    {
        var solutionPath = Path.Combine(RepositoryLocator.FindRoot(), "SmartHal.slnx");
        var solution = XDocument.Load(solutionPath);

        return solution
            .Descendants("Project")
            .Select(project => (string?)project.Attribute("Path"))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!.Replace('\\', '/'))
            .ToArray();
    }
}
