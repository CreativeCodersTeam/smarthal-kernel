using AwesomeAssertions;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using SmartHal.Core.Abstractions.Validation;
using SmartHal.Core.Validation;
using Xunit;

namespace SmartHal.Core.UnitTests.Validation;

/// <summary>
/// Verifies the data type rules: struct depth (R1), data type constraints (R2) and null entries, checked through a
/// reusable data type definition.
/// </summary>
public sealed class DataTypeRulesTests
{
    private readonly ContractValidator _sut = new ContractValidator();

    [Fact]
    public void Validate_StructTwoLevelsDeep_ReportsNothing()
    {
        // Arrange
        var definition = Define(Struct(("a", Struct(("b", new BooleanType())))));

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_StructThreeLevelsDeep_ReportsStructTooDeepAtTheThirdLevel()
    {
        // Arrange
        var definition = Define(Struct(("a", Struct(("b", Struct(("c", new BooleanType())))))));

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.StructTooDeep, "dataType.fields.a.fields.b"));
    }

    [Fact]
    public void Validate_StructInsideArray_CountsNoLevelForTheArray()
    {
        // Arrange
        var valid = Define(Struct(("a", new ArrayType(Struct(("x", new NumberType()))))));
        var invalid = Define(Struct(("a", new ArrayType(Struct(("x", Struct(("y", new NumberType()))))))));

        // Act
        var validErrors = _sut.Validate(valid);
        var invalidErrors = _sut.Validate(invalid);

        // Assert
        validErrors.Should().BeEmpty();
        invalidErrors.CodesAndPaths().Should().Equal((ValidationCodes.StructTooDeep, "dataType.fields.a.items.fields.x"));
    }

    [Fact]
    public void Validate_RequiredFieldThatIsNotDefined_ReportsUnknownField()
    {
        // Arrange
        var definition = Define(new ObjectType(new Dictionary<string, DataType> { ["h"] = new NumberType() }, ["h", "s"]));

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.UnknownField, "dataType.required[1]"));
    }

    [Fact]
    public void Validate_EnumWithoutValues_ReportsEmptyEnum()
    {
        // Arrange
        var definition = Define(new EnumType([]));

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.EmptyEnum, "dataType.values"));
    }

    [Fact]
    public void Validate_EnumWithRepeatedValue_ReportsEveryRepetition()
    {
        // Arrange
        var definition = Define(new EnumType(["on", "off", "on", "on"]));

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.DuplicateEnumValue, "dataType.values[2]"),
            (ValidationCodes.DuplicateEnumValue, "dataType.values[3]"));
    }

    [Fact]
    public void Validate_EnumWithDistinctValues_ReportsNothing()
    {
        // Arrange
        var definition = Define(new EnumType(["on", "On"]));

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.Should().BeEmpty("enum values are compared case-sensitively");
    }

    [Fact]
    public void Validate_MinimumAboveMaximum_ReportsInvalidRange()
    {
        // Arrange
        var definition = Define(new IntegerType(Minimum: 10, Maximum: 5));

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.InvalidRange, "dataType.minimum"));
    }

    [Fact]
    public void Validate_MinimumEqualToMaximum_ReportsNothing()
    {
        // Arrange
        var definition = Define(new NumberType(Minimum: 5, Maximum: 5));

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    public void Validate_StepNotAboveZero_ReportsInvalidStep(double step)
    {
        // Arrange
        var definition = Define(new NumberType(Step: step));

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.InvalidStep, "dataType.step"));
    }

    [Fact]
    public void Validate_NullEntries_ReportsNullEntryAndContinues()
    {
        // Arrange
        var definition = Define(
            new ObjectType(
                new Dictionary<string, DataType> { ["a"] = null!, ["b"] = new EnumType(["x", null!]) },
                [null!]));

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.NullEntry, "dataType.fields.a"),
            (ValidationCodes.NullEntry, "dataType.required[0]"),
            (ValidationCodes.NullEntry, "dataType.fields.b.values[1]"));
    }

    [Fact]
    public void Validate_NullDataTypeAndNullArrayItems_ReportsNullEntry()
    {
        // Arrange
        var missing = new DataTypeDef("x", new TypeVersion(1, 0), null!);
        var missingItems = Define(new ArrayType(null!));

        // Act
        var missingErrors = _sut.Validate(missing);
        var missingItemsErrors = _sut.Validate(missingItems);

        // Assert
        missingErrors.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, "dataType"));
        missingItemsErrors.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, "dataType.items"));
    }

    [Fact]
    public void Validate_RefTypeWithoutCatalog_IsNotResolved()
    {
        // Arrange
        var definition = Define(new RefType(new TypeRef("core.types.unknown", 1)));

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.Should().BeEmpty("references are resolved only against a catalog");
    }

    private static DataTypeDef Define(DataType dataType) => new DataTypeDef("test.types.sample", new TypeVersion(1, 0), dataType);

    private static ObjectType Struct(params (string Name, DataType Type)[] fields) =>
        new ObjectType(fields.ToDictionary(field => field.Name, field => field.Type));
}
