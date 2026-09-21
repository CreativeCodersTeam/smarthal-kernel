using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using AwesomeAssertions;
using Xunit;

namespace SmartHal.ArchitectureTests.Namespaces;

/// <summary>
/// Verifies the namespace bans against the production assemblies and proves each of them against a
/// probe type that deliberately violates it (FR-27, FR-58, FR-59, FR-60, AC-17, AC-21).
/// </summary>
public sealed class NamespaceRulesTests
{
    [Fact]
    public void NoMqttInKernelAndPublished_ProductionArchitecture_HasNoViolations()
    {
        // Arrange
        var rule = NamespaceRules.NoMqttInKernelAndPublished;

        // Act
        var failures = FailingTypeNames(rule, Architectures.Production);

        // Assert
        failures.Should().BeEmpty("ADR-0002 and FR-58 keep MQTT out of the kernel and the published projects");
    }

    [Fact]
    public void NoHomeAssistantInKernelAndPublished_ProductionArchitecture_HasNoViolations()
    {
        // Arrange
        var rule = NamespaceRules.NoHomeAssistantInKernelAndPublished;

        // Act
        var failures = FailingTypeNames(rule, Architectures.Production);

        // Assert
        failures.Should().BeEmpty("ADR-0003 and FR-59 keep Home Assistant out of the kernel and the published projects");
    }

    [Fact]
    public void SerilogOnlyInCompositionRoot_ProductionArchitecture_HasNoViolations()
    {
        // Arrange
        var rule = NamespaceRules.SerilogOnlyInCompositionRoot;

        // Act
        var failures = FailingTypeNames(rule, Architectures.Production);

        // Assert
        failures.Should().BeEmpty("FR-27 and FR-60 confine Serilog to the composition root of SmartHal.Server");
    }

    [Fact]
    public void SerilogOnlyInCompositionRoot_ProbeArchitecture_ReportsSerilogProbe()
    {
        // Arrange
        var rule = NamespaceRules.SerilogOnlyInCompositionRoot;
        string[] expectedProbeTypeNames = ["SmartHal.Server.Probes.SerilogProbe"];

        // Act
        var failures = FailingTypeNames(rule, Architectures.Probe);

        // Assert
        failures.Should().BeEquivalentTo(expectedProbeTypeNames);
    }

    [Theory]
    [InlineData("Mqtt", "SmartHal.Core.Probes.MqttProbe")]
    [InlineData("HomeAssistant", "SmartHal.Contracts.Probes.HomeAssistantProbe")]
    public void NamespaceRules_ProbeArchitecture_ReportMqttAndHomeAssistantProbes(
        string ruleName,
        string expectedProbeTypeName)
    {
        // Arrange
        var rule = ruleName switch
        {
            "Mqtt" => NamespaceRules.NoMqttInKernelAndPublished,
            "HomeAssistant" => NamespaceRules.NoHomeAssistantInKernelAndPublished,
            _ => throw new ArgumentOutOfRangeException(nameof(ruleName), ruleName, "Unknown namespace rule.")
        };
        string[] expectedProbeTypeNames = [expectedProbeTypeName];

        // Act
        var failures = FailingTypeNames(rule, Architectures.Probe);

        // Assert
        failures.Should().BeEquivalentTo(expectedProbeTypeNames);
    }

    private static string[] FailingTypeNames(IArchRule rule, Architecture architecture)
    {
        return rule
            .Evaluate(architecture)
            .Where(result => !result.Passed)
            .Select(result => (result.EvaluatedObject as IType)?.FullName ?? result.EvaluatedObjectIdentifier.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
