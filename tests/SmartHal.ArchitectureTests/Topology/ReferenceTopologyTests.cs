using AwesomeAssertions;
using Xunit;

namespace SmartHal.ArchitectureTests.Topology;

/// <summary>
/// Verifies that the reference topology data structure carries exactly the table of the
/// specification (IF-2, spec section 6.2).
/// </summary>
public sealed class ReferenceTopologyTests
{
    private static readonly string[] AllProjectNames =
    [
        "SmartHal.Contracts",
        "SmartHal.Adapter.Sdk",
        "SmartHal.Automation.Sdk",
        "SmartHal.Core.Abstractions",
        "SmartHal.Core",
        "SmartHal.Server",
        "SmartHal.Cli"
    ];

    [Theory]
    [InlineData("SmartHal.Contracts", "")]
    [InlineData("SmartHal.Adapter.Sdk", "SmartHal.Contracts")]
    [InlineData("SmartHal.Automation.Sdk", "SmartHal.Contracts")]
    [InlineData("SmartHal.Core.Abstractions", "SmartHal.Adapter.Sdk,SmartHal.Contracts")]
    [InlineData("SmartHal.Core", "SmartHal.Core.Abstractions,SmartHal.Adapter.Sdk,SmartHal.Contracts")]
    [InlineData(
        "SmartHal.Server",
        "SmartHal.Core,SmartHal.Core.Abstractions,SmartHal.Adapter.Sdk,SmartHal.Contracts")]
    [InlineData("SmartHal.Cli", "SmartHal.Contracts")]
    public void ReferenceTopology_Allowed_MatchesSpecTable(string projectName, string expectedReferences)
    {
        // Arrange
        var expected = expectedReferences
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Act
        var allowed = ReferenceTopology.Specification.Allowed;

        // Assert
        allowed.Keys.Should().BeEquivalentTo(AllProjectNames);
        allowed.Should().ContainKey(projectName);
        allowed[projectName].Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void ReferenceTopology_PublishedAndInternal_PartitionAllSevenProjects()
    {
        // Arrange
        var topology = ReferenceTopology.Specification;
        string[] expectedPublished = ["SmartHal.Contracts", "SmartHal.Adapter.Sdk", "SmartHal.Automation.Sdk"];
        string[] expectedInternal =
            ["SmartHal.Core.Abstractions", "SmartHal.Core", "SmartHal.Server", "SmartHal.Cli"];

        // Act
        var published = topology.Published;
        var projectsInternal = topology.Internal;

        // Assert
        published.Should().BeEquivalentTo(expectedPublished);
        projectsInternal.Should().BeEquivalentTo(expectedInternal);
        published.Concat(projectsInternal).Should().BeEquivalentTo(AllProjectNames);
    }
}
