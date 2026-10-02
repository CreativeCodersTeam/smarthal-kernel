using AwesomeAssertions;
using SmartHal.Contracts.Schema;
using SmartHal.Core.Catalog;
using SmartHal.Core.Validation;
using Xunit;

namespace SmartHal.Core.UnitTests.Catalog;

/// <summary>
/// Verifies that the core capability catalog satisfies every structural rule the contract validator checks.
/// </summary>
public sealed class CoreCatalogValidityTests
{
    private readonly ContractValidator _validator = new();

    public static TheoryData<string> CapabilityNames => [.. CoreCapabilityCatalog.All.Select(capability => capability.Name)];

    [Fact]
    public void Validate_TypeCatalogOfTheCoreCatalog_ReportsNoViolation()
    {
        // Arrange
        var catalog = CoreCapabilityCatalog.ToTypeCatalog();

        // Act
        var errors = _validator.Validate(catalog);

        // Assert
        errors.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(CapabilityNames))]
    public void Validate_EachCoreCapability_ReportsNoViolation(string name)
    {
        // Arrange
        CapabilityType capability = CoreCapabilityCatalog.All.Single(entry => entry.Name == name);

        // Act
        var errors = _validator.Validate(capability);

        // Assert
        errors.Should().BeEmpty($"{name} must satisfy R1 to R6");
    }

    [Fact]
    public void Validate_HsvDataType_ReportsNoViolation()
    {
        // Arrange
        var hsv = CoreDataTypes.Hsv;

        // Act
        var errors = _validator.Validate(hsv);

        // Assert
        errors.Should().BeEmpty();
    }
}
