using AwesomeAssertions;
using SmartHal.Contracts.Api;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;
using SmartHal.Core.Abstractions.Validation;
using SmartHal.Core.Validation;
using Xunit;

namespace SmartHal.Core.UnitTests.Validation;

/// <summary>
/// Verifies the reference analysis on the inputs a foreign catalog can bring: any order of the definitions, joined
/// and longer cycles, references without a name, incomplete referenced types, very long chains and nested patterns.
/// </summary>
public sealed class ReferenceGraphRobustnessTests
{
    private static readonly TypeVersion V1 = new TypeVersion(1, 0);

    private readonly ContractValidator _sut = new ContractValidator();

    [Fact]
    public void Validate_PointingTypeListedAfterTheCycle_ReportsTheCycleMembersOnly()
    {
        // Arrange
        var cycle = Define("vendor.cyc", Struct(("self", Ref("vendor.cyc"))));
        var outer = Define("vendor.outer", new ArrayType(Ref("vendor.cyc")));

        // Act
        var errors = _sut.Validate(DataTypesOnly(cycle, outer));

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.RefCycle, "dataTypes[0].dataType.fields.self"));
    }

    [Fact]
    public void Validate_ChainListedFromItsEnd_StillReportsTheTooDeepReference()
    {
        // Arrange
        // a > ref b > ref c, each a struct: four levels through a, three through b. Listed c, b, a.
        var c = Define("vendor.c", Struct(("x", Struct(("y", new BooleanType())))));
        var b = Define("vendor.b", Struct(("c", Ref("vendor.c"))));
        var a = Define("vendor.a", Struct(("b", Ref("vendor.b"))));

        // Act
        var errors = _sut.Validate(DataTypesOnly(c, b, a));

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.StructTooDeep, "dataTypes[1].dataType.fields.c"),
            (ValidationCodes.StructTooDeep, "dataTypes[2].dataType.fields.b"));
    }

    [Fact]
    public void Validate_CycleListedBeforeTheChainThatLeadsIntoIt_ReportsOnlyTheCycle()
    {
        // Arrange
        // c <> d listed first, then the chain a > b > c that only points into the cycle.
        var c = Define("vendor.c", Struct(("d", Ref("vendor.d"))));
        var d = Define("vendor.d", Struct(("c", Ref("vendor.c"))));
        var a = Define("vendor.a", new ArrayType(Ref("vendor.b")));
        var b = Define("vendor.b", new ArrayType(Ref("vendor.c")));

        // Act
        var errors = _sut.Validate(DataTypesOnly(c, d, a, b));

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.RefCycle, "dataTypes[0].dataType.fields.d"),
            (ValidationCodes.RefCycle, "dataTypes[1].dataType.fields.c"));
    }

    [Fact]
    public void Validate_TwoCyclesJoinedByOneWayReference_ReportsNoCycleAtTheJoin()
    {
        // Arrange
        // a <> b, b > c, c <> d: the reference b > c leads into another cycle but not back.
        var a = Define("vendor.a", new ArrayType(Ref("vendor.b")));
        var b = Define("vendor.b", Struct(("a", new ArrayType(Ref("vendor.a"))), ("c", new ArrayType(Ref("vendor.c")))));
        var c = Define("vendor.c", new ArrayType(Ref("vendor.d")));
        var d = Define("vendor.d", new ArrayType(Ref("vendor.c")));

        // Act
        var errors = _sut.Validate(DataTypesOnly(a, b, c, d));

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.RefCycle, "dataTypes[0].dataType.items"),
            (ValidationCodes.RefCycle, "dataTypes[1].dataType.fields.a.items"),
            (ValidationCodes.RefCycle, "dataTypes[2].dataType.items"),
            (ValidationCodes.RefCycle, "dataTypes[3].dataType.items"));
    }

    [Fact]
    public void Validate_ThreeTypeCycleWithOutsideReference_ReportsTheThreeCycleReferencesOnly()
    {
        // Arrange
        var a = Define("vendor.a", new ArrayType(Ref("vendor.b")));
        var b = Define("vendor.b", new ArrayType(Ref("vendor.c")));
        var c = Define("vendor.c", new ArrayType(Ref("vendor.a")));
        var d = Define("vendor.d", new ArrayType(Ref("vendor.a")));

        // Act
        var errors = _sut.Validate(DataTypesOnly(d, a, b, c));

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.RefCycle, "dataTypes[1].dataType.items"),
            (ValidationCodes.RefCycle, "dataTypes[2].dataType.items"),
            (ValidationCodes.RefCycle, "dataTypes[3].dataType.items"));
    }

    [Fact]
    public void Validate_CycleMemberWithDeepBranchOutsideTheCycle_ReportsCycleAndDepthInsideTheMember()
    {
        // Arrange
        // a <> b; b also holds a struct field that refers to c, which spans two levels on its own.
        var a = Define("vendor.a", new ArrayType(Ref("vendor.b")));
        var b = Define("vendor.b", Struct(("a", new ArrayType(Ref("vendor.a"))), ("c", Ref("vendor.c"))));
        var c = Define("vendor.c", Struct(("x", Struct(("y", new BooleanType())))));

        // Act
        var errors = _sut.Validate(DataTypesOnly(a, b, c));

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.RefCycle, "dataTypes[0].dataType.items"),
            (ValidationCodes.RefCycle, "dataTypes[1].dataType.fields.a.items"),
            (ValidationCodes.StructTooDeep, "dataTypes[1].dataType.fields.c"));
    }

    [Fact]
    public void Levels_InnerTypeMeasuredBeforeTheHead_GivesTheSameResultsAndExpandsEveryTypeOnce()
    {
        // Arrange
        var index = new CatalogIndex(DataTypesOnly(Chain(types: 10)), judgeMissingLists: false);

        // Act
        var inner = index.References.Levels(new TypeRef("chain.t5", 1));
        var head = index.References.Levels(new TypeRef("chain.t0", 1));

        // Assert
        inner.Should().Be(5);
        head.Should().Be(10);
        index.References.LevelExpansions.Should().Be(10);
    }

    [Fact]
    public void Validate_ChainOfTenThousandTypes_CompletesAndExpandsEveryTypeOnce()
    {
        // Arrange
        // t_i is a struct with one reference to t_{i+1}: t_{n-1} spans 1 level, t_{n-2} 2, and every reference from
        // t_0 to t_{n-3} reaches past two levels.
        const int types = 10_000;
        var catalog = DataTypesOnly(Chain(types));
        CatalogIndex? usedIndex = null;

        // Act
        var errors = ContractValidator.ValidateCatalog(catalog, index => usedIndex = index);

        // Assert
        // The counter belongs to the index of the validation run itself, so it proves what Validate expanded: the
        // targets of references, t1 to t_{n-1}, each exactly once; the head t0 is no target.
        errors.Should().HaveCount(types - 2).And.OnlyContain(error => error.Code == ValidationCodes.StructTooDeep);
        usedIndex!.References.LevelExpansions.Should().Be(types - 1);
        usedIndex.References.Levels(new TypeRef("chain.t0", 1)).Should().Be(types, "the head spans the whole chain");
        usedIndex.References.LevelExpansions.Should().Be(types, "measuring the head adds only the head itself");
    }

    [Fact]
    public void Validate_CatalogWithCycleThenCatalogWithout_CarriesNothingOver()
    {
        // Arrange
        var cyclic = DataTypesOnly(Define("vendor.node", Struct(("next", Ref("vendor.node")))));
        var acyclic = DataTypesOnly(Define("vendor.node", Struct(("next", new BooleanType()))));

        // Act
        var first = _sut.Validate(cyclic);
        var second = _sut.Validate(acyclic);

        // Assert
        first.CodesAndPaths().Should().Equal((ValidationCodes.RefCycle, "dataTypes[0].dataType.fields.next"));
        second.Should().BeEmpty();
    }

    [Fact]
    public void Validate_AliasOfTypeWithTwoLevels_ReportsNoViolation()
    {
        // Arrange
        var alias = Define("vendor.alias", Ref("vendor.two"));
        var two = Define("vendor.two", Struct(("x", Struct(("y", new BooleanType())))));

        // Act
        var errors = _sut.Validate(DataTypesOnly(alias, two));

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_AliasChainToTypeWithThreeLevels_ReportsStructTooDeepAtEachAlias()
    {
        // Arrange
        // a > ref b > ref c; c spans three levels on its own and is reported where it is defined.
        var a = Define("vendor.a", Ref("vendor.b"));
        var b = Define("vendor.b", Ref("vendor.c"));
        var c = Define("vendor.c", Struct(("x", Struct(("y", Struct(("z", new BooleanType())))))));

        // Act
        var errors = _sut.Validate(DataTypesOnly(a, b, c));

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.StructTooDeep, "dataTypes[0].dataType"),
            (ValidationCodes.StructTooDeep, "dataTypes[1].dataType"),
            (ValidationCodes.StructTooDeep, "dataTypes[2].dataType.fields.x.fields.y"));
        errors[0].Message.Should().Contain("3 levels");
        errors[1].Message.Should().Contain("3 levels");
    }

    [Fact]
    public void Validate_SelfReferenceWithoutCatalog_ReportsNoViolation()
    {
        // Arrange
        var node = Define("vendor.node", Struct(("next", Ref("vendor.node"))));

        // Act
        var errors = _sut.Validate(node);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ReferenceWithoutName_ReportsNullEntryAtTheReference()
    {
        // Arrange
        var holder = Define("vendor.holder", Struct(("x", new RefType(default))));

        // Act
        var withCatalog = _sut.Validate(DataTypesOnly(holder));
        var withoutCatalog = _sut.Validate(holder);

        // Assert
        withCatalog.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, "dataTypes[0].dataType.fields.x.ref"));
        withoutCatalog.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, "dataType.fields.x.ref"));
    }

    [Fact]
    public void Validate_CapabilityPropertyReferenceWithoutName_ReportsNullEntry()
    {
        // Arrange
        var capability = new CapabilityType(
            "vendor.cap",
            V1,
            new Dictionary<string, PropertyDef> { ["value"] = new PropertyDef(new RefType(default), PropertyCategory.State) },
            new Dictionary<string, CommandDef>(),
            new Dictionary<string, EventDef>(),
            new Dictionary<string, AlarmDef>());

        // Act
        var errors = _sut.Validate(new TypeCatalog([capability], [], [], []));

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, "capabilities[0].properties.value.dataType.ref"));
    }

    public static TheoryData<DataType?, int> IncompleteTargets =>
        new TheoryData<DataType?, int>
    {
        { null, 0 },
        { new ObjectType(null!), 1 },
        { new ObjectType(new Dictionary<string, DataType> { ["x"] = null! }), 1 },
        { new ArrayType(null!), 0 }
    };

    [Theory]
    [MemberData(nameof(IncompleteTargets))]
    public void Levels_IncompleteReferencedType_CountsItsNaturalLevelsWithoutThrowing(DataType? target, int expectedLevels)
    {
        // Arrange
        var catalog = DataTypesOnly(Define("vendor.holder", Ref("vendor.target")), new DataTypeDef("vendor.target", V1, target!));
        var index = new CatalogIndex(catalog, judgeMissingLists: false);

        // Act
        var levels = index.References.Levels(new TypeRef("vendor.target", 1));
        var errors = _sut.Validate(catalog);

        // Assert
        levels.Should().Be(expectedLevels);
        errors.Should().OnlyContain(error => error.Code == ValidationCodes.NullEntry);
    }

    [Fact]
    public void Validate_EmptyPattern_ReportsNoViolation()
    {
        // Arrange
        var definition = Define("vendor.code", new StringType(Pattern: ""));

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidPatternsNestedInFieldAndItems_ReportedAtTheNestedPaths()
    {
        // Arrange
        var definition = Define(
            "vendor.codes",
            Struct(("x", new StringType(Pattern: "(")), ("list", new ArrayType(new StringType(Pattern: "[a-")))));

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.Should().Equal(
            new ValidationError("dataType.fields.x.pattern", ValidationCodes.InvalidPattern,
                "The pattern '(' is not a valid regular expression."),
            new ValidationError("dataType.fields.list.items.pattern", ValidationCodes.InvalidPattern,
                "The pattern '[a-' is not a valid regular expression."));
    }

    [Theory]
    [InlineData("(?<y>[0-9]{4})")]
    [InlineData("(?<=a)b")]
    public void Validate_NamedGroupOrLookbehindPattern_ReportsNoViolation(string pattern)
    {
        // Arrange
        // Observed .NET behavior: the ECMAScript mode of System.Text.RegularExpressions accepts named groups and
        // lookbehind, as ECMA-262 (ES2018) and therefore JSON Schema do. The mode changes matching, not the syntax
        // check; no pattern was found that is valid in the default mode but rejected in ECMAScript mode.
        var definition = Define("vendor.code", new StringType(Pattern: pattern));

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_SelfReferencingDuplicateThatLosesOnMinor_ReportsOnlyTheDuplicate()
    {
        // Arrange
        // core.x 1.0 refers to core.x@1, which resolves to the winning 1.2; the losing 1.0 is not in the reference
        // graph, so its reference closes no cycle of its own.
        var loser = new DataTypeDef("core.x", new TypeVersion(1, 0), Struct(("self", Ref("core.x"))));
        var winner = new DataTypeDef("core.x", new TypeVersion(1, 2), Struct(("value", new NumberType())));

        // Act
        var errors = _sut.Validate(DataTypesOnly(loser, winner));

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.DuplicateKey, "dataTypes[1].name"));
    }

    [Fact]
    public void Validate_SelfReferencingDuplicateThatWinsOnMinor_ReportsDuplicateAndItsCycle()
    {
        // Arrange
        var winner = new DataTypeDef("core.x", new TypeVersion(1, 2), Struct(("self", Ref("core.x"))));
        var loser = new DataTypeDef("core.x", new TypeVersion(1, 0), Struct(("value", new NumberType())));

        // Act
        var errors = _sut.Validate(DataTypesOnly(winner, loser));

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.DuplicateKey, "dataTypes[1].name"),
            (ValidationCodes.RefCycle, "dataTypes[0].dataType.fields.self"));
    }

    [Fact]
    public void Validate_EqualDuplicatesWithTheSelfReferenceInTheSecond_ReportsOnlyTheDuplicate()
    {
        // Arrange
        // On an equal minor version the first entry wins, so the second one's reference closes no cycle of its own.
        var first = new DataTypeDef("core.x", V1, Struct(("value", new NumberType())));
        var second = new DataTypeDef("core.x", V1, Struct(("self", Ref("core.x"))));

        // Act
        var errors = _sut.Validate(DataTypesOnly(first, second));

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.DuplicateKey, "dataTypes[1].name"));
    }

    [Fact]
    public void Validate_EqualDuplicatesWithTheSelfReferenceInTheFirst_ReportsTheCycleAtTheFirst()
    {
        // Arrange
        var first = new DataTypeDef("core.x", V1, Struct(("self", Ref("core.x"))));
        var second = new DataTypeDef("core.x", V1, Struct(("value", new NumberType())));

        // Act
        var errors = _sut.Validate(DataTypesOnly(first, second));

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.DuplicateKey, "dataTypes[1].name"),
            (ValidationCodes.RefCycle, "dataTypes[0].dataType.fields.self"));
    }

    [Fact]
    public void Validate_WinnerAndLoserBothSelfReferencing_ReportsTheCycleAtTheWinnerOnly()
    {
        // Arrange
        var loser = new DataTypeDef("core.x", new TypeVersion(1, 0), Struct(("self", Ref("core.x"))));
        var winner = new DataTypeDef("core.x", new TypeVersion(1, 2), Struct(("self", Ref("core.x"))));

        // Act
        var errors = _sut.Validate(DataTypesOnly(loser, winner));

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.DuplicateKey, "dataTypes[1].name"),
            (ValidationCodes.RefCycle, "dataTypes[1].dataType.fields.self"));
    }

    [Fact]
    public void Validate_SelfReferencingLoserListedAfterTheWinner_ReportsOnlyTheDuplicate()
    {
        // Arrange
        var winner = new DataTypeDef("core.x", new TypeVersion(1, 2), Struct(("value", new NumberType())));
        var loser = new DataTypeDef("core.x", new TypeVersion(1, 0), Struct(("self", Ref("core.x"))));

        // Act
        var errors = _sut.Validate(DataTypesOnly(winner, loser));

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.DuplicateKey, "dataTypes[1].name"));
    }

    [Fact]
    public void Validate_SameSelfReferencingInstanceListedTwice_ReportsTheCycleAtBothPositions()
    {
        // Arrange
        // Both positions hold the very definition the catalog resolves to, so both close the cycle.
        var definition = new DataTypeDef("core.x", V1, Struct(("self", Ref("core.x"))));

        // Act
        var errors = _sut.Validate(DataTypesOnly(definition, definition));

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.DuplicateKey, "dataTypes[1].name"),
            (ValidationCodes.RefCycle, "dataTypes[0].dataType.fields.self"),
            (ValidationCodes.RefCycle, "dataTypes[1].dataType.fields.self"));
    }

    [Fact]
    public void Validate_LosingDuplicateInAMutualCycle_ReportsTheCycleAtTheWinnerAndItsPartnerOnly()
    {
        // Arrange
        // The cycle runs between the winning a@1.2 and b; the losing a@1.0 only points into it.
        var loser = new DataTypeDef("core.a", new TypeVersion(1, 0), Struct(("next", Ref("core.b"))));
        var partner = new DataTypeDef("core.b", V1, Struct(("back", Ref("core.a"))));
        var winner = new DataTypeDef("core.a", new TypeVersion(1, 2), Struct(("next", Ref("core.b"))));

        // Act
        var errors = _sut.Validate(DataTypesOnly(loser, partner, winner));

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.DuplicateKey, "dataTypes[2].name"),
            (ValidationCodes.RefCycle, "dataTypes[1].dataType.fields.back"),
            (ValidationCodes.RefCycle, "dataTypes[2].dataType.fields.next"));
    }

    [Fact]
    public void Validate_UnnamedOwnerReferencingANamedType_DoesNotThrow()
    {
        // Arrange
        var unnamed = new DataTypeDef(null!, V1, Struct(("other", Ref("core.x"))));
        var named = new DataTypeDef("core.x", V1, Struct(("back", Ref("core.x"))));

        // Act
        var act = () => _sut.Validate(DataTypesOnly(unnamed, named));

        // Assert
        act.Should().NotThrow().Which.CodesAndPaths().Should().Contain(
            [(ValidationCodes.NullEntry, "dataTypes[0].name"), (ValidationCodes.RefCycle, "dataTypes[1].dataType.fields.back")]);
    }

    // chain.t0 .. chain.t{types-1}: every type is a struct with one field; all but the last refer to the next type.
    private static DataTypeDef[] Chain(int types) =>
        [.. Enumerable.Range(0, types).Select(i => Define(
            $"chain.t{i}",
            Struct(("next", i + 1 < types ? Ref($"chain.t{i + 1}") : new BooleanType()))))];

    private static TypeCatalog DataTypesOnly(params DataTypeDef[] definitions) => new TypeCatalog([], [], [], definitions);

    private static DataTypeDef Define(string name, DataType dataType) => new DataTypeDef(name, V1, dataType);

    private static RefType Ref(string name) => new RefType(new TypeRef(name, 1));

    private static ObjectType Struct(params (string Name, DataType Type)[] fields) =>
        new ObjectType(fields.ToDictionary(field => field.Name, field => field.Type));
}
