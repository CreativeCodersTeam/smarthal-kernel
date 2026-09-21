using System.Reflection;
using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartHal.Server.Configuration;
using Xunit;

namespace SmartHal.Server.UnitTests.Configuration;

/// <summary>
/// Verifies that a validation failure is turned into section, field and reason - and into nothing
/// else (FR-24, G-9).
/// </summary>
public sealed class OptionsFailureTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"smarthal-failure-{Guid.NewGuid():N}");

    public OptionsFailureTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void Parse_DataAnnotationsAndCustomFailures_YieldSectionFieldReasonWithoutValue()
    {
        // Arrange
        // The first message is the form DataAnnotations produces, the second the form the validator
        // of this section produces, the third a DataAnnotations message naming two members at once.
        var exception = new OptionsValidationException(
            Options.DefaultName,
            typeof(SmartHalOptions),
            [
                "DataAnnotation validation failed for 'SmartHalOptions' members: 'InstanceName' with the error: 'The InstanceName field is required.'.",
                "SmartHal:ShutdownTimeout: must be greater than zero.",
                "DataAnnotation validation failed for 'SmartHalOptions' members: 'DataDirectory,InstanceName' with the error: 'The field is required.'."
            ]);

        // Act
        var failures = OptionsFailure.Parse(exception);

        // Assert
        failures.Should().HaveCount(4, "a message naming two members reports two violations");
        failures.Should().AllSatisfy(failure => failure.Section.Should().Be("SmartHal"));
        failures.Select(failure => failure.Field).Should().BeEquivalentTo(
            "InstanceName",
            "ShutdownTimeout",
            "DataDirectory",
            "InstanceName");
        failures.Should().Contain(failure =>
            failure.Field == "ShutdownTimeout" && failure.Reason == "must be greater than zero.");
        failures.Should().Contain(failure =>
            failure.Field == "InstanceName" && failure.Reason == "The InstanceName field is required.");

        // G-9: the record has room for the section, the field and the reason and for nothing else, so
        // no offending value can travel with it.
        typeof(OptionsFailure)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Should().BeEquivalentTo("Section", "Field", "Reason");

        // The same holds for a failure that a real validation run produced from a configured value.
        var configuredValue = new string('x', 65);
        var validator = new SmartHalOptionsValidator(A.Fake<ILogger<SmartHalOptionsValidator>>());
        var result = validator.Validate(
            Options.DefaultName,
            new SmartHalOptions { InstanceName = configuredValue, DataDirectory = _tempDirectory });
        var realException = new OptionsValidationException(
            Options.DefaultName,
            typeof(SmartHalOptions),
            result.Failures);

        OptionsFailure.Parse(realException).Should().AllSatisfy(failure =>
            (failure.Section + failure.Field + failure.Reason).Should().NotContain(
                configuredValue,
                "in slice 0 no configured value appears in a validation message (G-9)"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
