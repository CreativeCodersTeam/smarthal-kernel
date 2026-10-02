using AwesomeAssertions;
using SmartHal.Contracts.Api;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;
using SmartHal.Core.Abstractions.Validation;
using SmartHal.Core.Validation;
using Xunit;
using static SmartHal.Core.UnitTests.Validation.ValidationFixtures;

namespace SmartHal.Core.UnitTests.Validation;

/// <summary>
/// Verifies that rule R1 follows references to reusable data types when a catalog is at hand: a reference counts the
/// struct levels of the type it points to, a reference that leads back to itself is a cycle, and an unresolved
/// reference adds no level.
/// </summary>
public sealed class ReferenceDepthTests
{
    private static readonly TypeVersion V1 = new TypeVersion(1, 0);

    private readonly ContractValidator _sut = new ContractValidator();

    [Fact]
    public void Validate_FourStructLevelsThroughReferences_ReportsStructTooDeepAtEachReference()
    {
        // Arrange
        // a > ref b; b > ref c; c is a struct with a nested struct (two levels on its own).
        var a = Define("vendor.a", Struct(("b", Ref("vendor.b"))));
        var b = Define("vendor.b", Struct(("c", Ref("vendor.c"))));
        var c = Define("vendor.c", Struct(("x", Struct(("y", new BooleanType())))));

        // Act
        var errors = _sut.Validate(DataTypesOnly(a, b, c));

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.StructTooDeep, "dataTypes[0].dataType.fields.b"),
            (ValidationCodes.StructTooDeep, "dataTypes[1].dataType.fields.c"));
        errors[0].Message.Should().Contain("4 levels");
        errors[1].Message.Should().Contain("3 levels");
    }

    [Fact]
    public void Validate_TwoStructLevelsThroughAReference_ReportsNoViolation()
    {
        // Arrange
        var color = Define("vendor.color", Struct(("hsv", new RefType(HsvRef))));

        // Act
        var errors = _sut.Validate(DataTypesOnly(color, Hsv()));

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_StructThatRefersToItself_ReportsRefCycle()
    {
        // Arrange
        var node = Define("vendor.node", Struct(("next", Ref("vendor.node"))));

        // Act
        var errors = _sut.Validate(DataTypesOnly(node));

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.RefCycle, "dataTypes[0].dataType.fields.next"));
    }

    [Fact]
    public void Validate_DataTypeThatIsAReferenceToItself_ReportsRefCycle()
    {
        // Arrange
        var alias = Define("vendor.alias", Ref("vendor.alias"));

        // Act
        var errors = _sut.Validate(DataTypesOnly(alias));

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.RefCycle, "dataTypes[0].dataType"));
    }

    [Fact]
    public void Validate_MutualReferences_ReportsRefCycleAtBothEntryPoints()
    {
        // Arrange
        var a = Define("vendor.a", Struct(("b", Ref("vendor.b"))));
        var b = Define("vendor.b", Struct(("a", Ref("vendor.a"))));

        // Act
        var errors = _sut.Validate(DataTypesOnly(a, b));

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.RefCycle, "dataTypes[0].dataType.fields.b"),
            (ValidationCodes.RefCycle, "dataTypes[1].dataType.fields.a"));
    }

    [Fact]
    public void Validate_ReferenceInsideAnArray_CountsTheLevelsOfTheReferencedStruct()
    {
        // Arrange
        var list = Define("vendor.list", Struct(("entries", new ArrayType(Ref("vendor.pair")))));
        var pair = Define("vendor.pair", Struct(("key", Struct(("value", new BooleanType())))));

        // Act
        var errors = _sut.Validate(DataTypesOnly(list, pair));

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.StructTooDeep, "dataTypes[0].dataType.fields.entries.items"));
    }

    [Fact]
    public void Validate_CycleThroughAnArray_ReportsRefCycle()
    {
        // Arrange
        var tree = Define("vendor.tree", Struct(("children", new ArrayType(Ref("vendor.tree")))));

        // Act
        var errors = _sut.Validate(DataTypesOnly(tree));

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.RefCycle, "dataTypes[0].dataType.fields.children.items"));
    }

    [Fact]
    public void Validate_ReferenceThatOnlyPointsIntoACycle_ReportsTheCycleMembersOnly()
    {
        // Arrange
        // cyc refers to itself; outer refers to cyc but is not part of the cycle.
        var outer = Define("x.outer", new ArrayType(Ref("x.cyc")));
        var cyc = Define("x.cyc", Struct(("next", Ref("x.cyc"))));

        // Act
        var errors = _sut.Validate(DataTypesOnly(outer, cyc));

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.RefCycle, "dataTypes[1].dataType.fields.next"));
    }

    [Fact]
    public void Validate_CapabilityPropertyReferringToACyclicType_ReportsNoCycleAtTheProperty()
    {
        // Arrange
        var cyc = Define("x.cyc", Struct(("next", Ref("x.cyc"))));
        var capability = Level() with
        {
            Properties = new Dictionary<string, PropertyDef> { ["level"] = new PropertyDef(Ref("x.cyc"), PropertyCategory.State) }
        };

        // Act
        var errors = _sut.Validate(new TypeCatalog([capability], [], [], [cyc]));

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.RefCycle, "dataTypes[0].dataType.fields.next"));
    }

    [Fact]
    public void Validate_ChainOfTypesWithEightReferencesEach_ExpandsEveryTypeOnce()
    {
        // Arrange
        // The probe of the second review: each type refers eight times to the next one. Expanding every path would
        // take 8^9 steps; expanding every type once takes ten.
        var catalog = DataTypesOnly(Chain(types: 10, referencesPerType: 8));
        var index = new CatalogIndex(catalog, judgeMissingLists: false);

        // Act
        var levels = index.References.Levels(new TypeRef("probe.t0", 1));

        // Assert
        levels.Should().Be(10, "every type of the chain is one struct level");
        index.References.LevelExpansions.Should().Be(10);
    }

    [Fact]
    public void Validate_LongChainOfTypesWithTwoReferencesEach_ExpandsEveryTypeOnce()
    {
        // Arrange
        var catalog = DataTypesOnly(Chain(types: 24, referencesPerType: 2));
        var index = new CatalogIndex(catalog, judgeMissingLists: false);

        // Act
        var levels = index.References.Levels(new TypeRef("probe.t0", 1));

        // Assert
        levels.Should().Be(24);
        index.References.LevelExpansions.Should().Be(24);
    }

    [Fact]
    public void Validate_ProbeCatalogOfTheReview_ReportsEveryTooDeepReferenceQuickly()
    {
        // Arrange
        var catalog = DataTypesOnly(Chain(types: 10, referencesPerType: 8));
        CatalogIndex? usedIndex = null;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var errors = ContractValidator.ValidateCatalog(catalog, index => usedIndex = index);
        stopwatch.Stop();

        // Assert
        // Type i holds eight references at depth 1 to a chain of 9 - i levels; that is too deep for i < 8.
        errors.Should().HaveCount(64).And.OnlyContain(error => error.Code == ValidationCodes.StructTooDeep);
        // The run measures the targets of references: t1 to t9, each exactly once; the head t0 is no target.
        usedIndex!.References.LevelExpansions.Should().Be(9, "the validation run itself expands every referenced type once");
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1), "the review measured 14.1 s before the cache");
    }

    [Fact]
    public void Validate_UnresolvedReference_ReportsOnlyUnresolvedType()
    {
        // Arrange
        var wrapper = Define("vendor.wrapper", Struct(("inner", Struct(("x", Ref("vendor.missing"))))));

        // Act
        var errors = _sut.Validate(DataTypesOnly(wrapper));

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.UnresolvedType, "dataTypes[0].dataType.fields.inner.fields.x.ref"));
    }

    [Fact]
    public void Validate_CapabilityPropertyTooDeepThroughAReference_ReportsStructTooDeep()
    {
        // Arrange
        var level = Level();
        var properties = new Dictionary<string, PropertyDef>(level.Properties)
        {
            ["palette"] = new PropertyDef(Struct(("primary", new RefType(HsvRef)), ("nested", Struct(("color", new RefType(HsvRef))))),
                PropertyCategory.State)
        };
        // Major 2 stays in the catalog because the migration of the test catalog points to it.
        var catalog = TestCatalog() with
        {
            Capabilities = [level with { Properties = properties }, level with { Version = new TypeVersion(2, 0) }]
        };

        // Act
        var errors = _sut.Validate(catalog);

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.StructTooDeep, "capabilities[0].properties.palette.dataType.fields.nested.fields.color"));
    }

    [Fact]
    public void Validate_ReferenceWithoutCatalog_IsNotExpandedForTheDepth()
    {
        // Arrange
        // Without a catalog the reference cannot be resolved, so only the levels of the definition itself count.
        var definition = Define(
            "vendor.acme.nested",
            Struct(("b", Struct(("color", new RefType(HsvRef))))));

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.Should().BeEmpty();
    }

    // probe.t0 .. probe.t{types-1}: every type is a struct whose fields all refer to the next type; the last one
    // holds booleans.
    private static DataTypeDef[] Chain(int types, int referencesPerType) =>
        [.. Enumerable.Range(0, types).Select(i => Define(
            $"probe.t{i}",
            Struct([.. Enumerable.Range(0, referencesPerType).Select(f => (
                $"f{f}",
                i + 1 < types ? (DataType)Ref($"probe.t{i + 1}") : new BooleanType()))])))];

    private static TypeCatalog DataTypesOnly(params DataTypeDef[] definitions) => new TypeCatalog([], [], [], definitions);

    private static DataTypeDef Define(string name, DataType dataType) => new DataTypeDef(name, V1, dataType);

    private static RefType Ref(string name) => new RefType(new TypeRef(name, 1));

    private static ObjectType Struct(params (string Name, DataType Type)[] fields) =>
        new ObjectType(fields.ToDictionary(field => field.Name, field => field.Type));
}
