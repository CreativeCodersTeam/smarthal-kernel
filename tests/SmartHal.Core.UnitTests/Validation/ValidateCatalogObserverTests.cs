using AwesomeAssertions;
using SmartHal.Contracts.Api;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;
using SmartHal.Core.Validation;
using Xunit;

namespace SmartHal.Core.UnitTests.Validation;

/// <summary>
/// Verifies the internal observer of a catalog run: it sees the index of exactly this run, once, before any entry is
/// checked.
/// </summary>
public sealed class ValidateCatalogObserverTests
{
    private static readonly TypeVersion V1 = new(1, 0);

    public static TheoryData<TypeCatalog> Catalogs => new()
    {
        new TypeCatalog([], [], []),
        new TypeCatalog(null!, null!, null!),
        new TypeCatalog([], [], [], [Define("core.a", Struct(("b", Ref("core.b")))), Define("core.b", Struct(("v", new NumberType())))])
    };

    [Theory]
    [MemberData(nameof(Catalogs))]
    public void ValidateCatalog_AnyCatalog_InvokesTheObserverExactlyOnce(TypeCatalog catalog)
    {
        // Arrange
        var calls = 0;

        // Act
        ContractValidator.ValidateCatalog(catalog, _ => calls++);

        // Assert
        calls.Should().Be(1);
    }

    [Fact]
    public void ValidateCatalog_CatalogWithReferences_ObservesTheIndexBeforeAnyEntryIsChecked()
    {
        // Arrange
        var catalog = new TypeCatalog(
            [],
            [],
            [],
            [Define("core.a", Struct(("b", Ref("core.b")))), Define("core.b", Struct(("v", new NumberType())))]);
        CatalogIndex? observed = null;
        var expansionsWhenObserved = -1;

        // Act
        ContractValidator.ValidateCatalog(catalog, index =>
        {
            observed = index;
            expansionsWhenObserved = index.References.LevelExpansions;
        });

        // Assert
        expansionsWhenObserved.Should().Be(0, "no entry has been checked when the observer runs");
        observed!.References.LevelExpansions.Should().BeGreaterThan(0, "the run expands the referenced type afterwards");
    }

    [Fact]
    public void ValidateCatalog_NullCatalog_ThrowsWithoutInvokingTheObserver()
    {
        // Arrange
        var calls = 0;

        // Act
        var act = () => ContractValidator.ValidateCatalog(null!, _ => calls++);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("catalog");
        calls.Should().Be(0);
    }

    [Fact]
    public void ValidateCatalog_TwoRuns_ObserveDistinctIndexes()
    {
        // Arrange
        var catalog = new TypeCatalog([], [], [], [Define("core.a", Struct(("v", new NumberType())))]);
        CatalogIndex? first = null;
        CatalogIndex? second = null;

        // Act
        ContractValidator.ValidateCatalog(catalog, index => first = index);
        ContractValidator.ValidateCatalog(catalog, index => second = index);

        // Assert
        first.Should().NotBeNull();
        second.Should().NotBeNull().And.NotBeSameAs(first);
    }

    private static DataTypeDef Define(string name, DataType dataType) => new(name, V1, dataType);

    private static RefType Ref(string name) => new(new TypeRef(name, 1));

    private static ObjectType Struct(params (string Name, DataType Type)[] fields) =>
        new(fields.ToDictionary(field => field.Name, field => field.Type));
}
