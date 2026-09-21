using AwesomeAssertions;
using SmartHal.ArchitectureTests.Solution;
using Xunit;

namespace SmartHal.ArchitectureTests.Topology;

/// <summary>
/// Verifies the reference topology rules against the real project graph and against synthetic
/// graphs that carry a deliberate violation
/// (FR-4 through FR-10, FR-55, FR-56, FR-57, AC-18, AC-19, AC-20).
/// </summary>
public sealed class TopologyRulesTests
{
    [Fact]
    public void FindForbiddenReferences_GraphWithUnlistedReference_NamesTheReference()
    {
        // Arrange
        var graph = GraphWith("SmartHal.Core", "SmartHal.Server");

        // Act
        var violations = TopologyRules.FindForbiddenReferences(graph, ReferenceTopology.Specification);

        // Assert
        violations.Should().ContainSingle();
        violations[0].From.Should().Be("SmartHal.Core");
        violations[0].To.Should().Be("SmartHal.Server");
        violations[0].ToString().Should().Contain("SmartHal.Core").And.Contain("SmartHal.Server");
    }

    [Theory]
    [InlineData("SmartHal.Adapter.Sdk", "SmartHal.Core.Abstractions")]
    [InlineData("SmartHal.Contracts", "SmartHal.Core")]
    [InlineData("SmartHal.Automation.Sdk", "SmartHal.Server")]
    public void FindPublishedToInternal_AdapterSdkReferencingCoreAbstractions_Fails(string from, string to)
    {
        // Arrange
        var graph = GraphWith(from, to);

        // Act
        var violations = TopologyRules.FindPublishedToInternal(graph, ReferenceTopology.Specification);

        // Assert
        violations.Should().ContainSingle();
        violations[0].From.Should().Be(from);
        violations[0].To.Should().Be(to);
        violations[0].ToString().Should().Contain(from).And.Contain(to);
    }

    [Theory]
    [InlineData("SmartHal.Adapter.Sdk", "SmartHal.Automation.Sdk")]
    [InlineData("SmartHal.Automation.Sdk", "SmartHal.Adapter.Sdk")]
    public void FindSdkCrossReferences_SdksReferencingEachOther_Fails(string from, string to)
    {
        // Arrange
        var graph = GraphWith(from, to);

        // Act
        var violations = TopologyRules.FindSdkCrossReferences(graph, ReferenceTopology.Specification);

        // Assert
        violations.Should().ContainSingle();
        violations[0].From.Should().Be(from);
        violations[0].To.Should().Be(to);
        violations[0].ToString().Should().Contain(from).And.Contain(to);
    }

    [Fact]
    public void FindForbiddenReferences_RealProjectGraph_ReportsNothing()
    {
        // Arrange
        var graph = RealProjectGraph();

        // Act
        var violations = TopologyRules.FindForbiddenReferences(graph, ReferenceTopology.Specification);

        // Assert
        violations.Should().BeEmpty(
            "FR-4 and FR-55 declare the reference table of section 6.2 exhaustive");
    }

    [Fact]
    public void FindPublishedToInternal_RealProjectGraph_ReportsNothing()
    {
        // Arrange
        var graph = RealProjectGraph();

        // Act
        var violations = TopologyRules.FindPublishedToInternal(graph, ReferenceTopology.Specification);

        // Assert
        violations.Should().BeEmpty("FR-9 and FR-56 forbid a published project to reference an internal one");
    }

    [Fact]
    public void FindSdkCrossReferences_RealProjectGraph_ReportsNothing()
    {
        // Arrange
        var graph = RealProjectGraph();

        // Act
        var violations = TopologyRules.FindSdkCrossReferences(graph, ReferenceTopology.Specification);

        // Assert
        violations.Should().BeEmpty("FR-8 and FR-57 forbid references between the two SDKs");
    }

    [Fact]
    public void RealProjectGraph_Contracts_HasNoSmartHalReference()
    {
        // Arrange
        var graph = RealProjectGraph();

        // Act
        var references = graph.References["SmartHal.Contracts"];

        // Assert
        references.Should().BeEmpty("FR-5 forbids SmartHal.Contracts to reference another SmartHal project");
    }

    [Fact]
    public void RealProjectGraph_AdapterSdk_ReferencesOnlyContracts()
    {
        // Arrange
        var graph = RealProjectGraph();

        // Act
        var references = graph.References["SmartHal.Adapter.Sdk"];

        // Assert
        references.Should().BeEquivalentTo(["SmartHal.Contracts"], "FR-6");
    }

    [Fact]
    public void RealProjectGraph_AutomationSdk_ReferencesOnlyContracts()
    {
        // Arrange
        var graph = RealProjectGraph();

        // Act
        var references = graph.References["SmartHal.Automation.Sdk"];

        // Assert
        references.Should().BeEquivalentTo(["SmartHal.Contracts"], "FR-7");
    }

    [Fact]
    public void RealProjectGraph_CoreAbstractions_ReferencesAdapterSdkAndContractsOnly()
    {
        // Arrange
        var graph = RealProjectGraph();

        // Act
        var references = graph.References["SmartHal.Core.Abstractions"];

        // Assert
        references.Should().BeEquivalentTo(["SmartHal.Adapter.Sdk", "SmartHal.Contracts"], "FR-10");
    }

    private static ProjectGraph RealProjectGraph()
    {
        return ProjectGraph.ReadFromSource(RepositoryLocator.FindRoot());
    }

    private static ProjectGraph GraphWith(string from, string to)
    {
        var references = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [from] = new HashSet<string>(StringComparer.Ordinal) { to }
        };

        return new ProjectGraph(references);
    }
}
