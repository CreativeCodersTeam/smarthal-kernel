using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using static SmartHal.Core.Catalog.CatalogDefinitions;

namespace SmartHal.Core.Catalog;

/// <summary>
/// Provides the reusable data types of the core catalog.
/// </summary>
/// <remarks>Every property returns a new, independent instance on each access.</remarks>
public static class CoreDataTypes
{
    /// <summary>
    /// Gets the reusable data type <c>core.types.hsv@1</c>: a color as hue, saturation and value.
    /// </summary>
    /// <value>
    /// A struct with the mandatory fields <c>h</c> (0 to 360 degrees), <c>s</c> and <c>v</c> (0 to 100 percent each).
    /// </value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static DataTypeDef Hsv => new(
        "core.types.hsv",
        CoreVersion,
        new ObjectType(
            Map<DataType>(
                ("h", new NumberType(Units.Degree, 0, 360)),
                ("s", Percentage()),
                ("v", Percentage())),
            ["h", "s", "v"]));

    /// <summary>
    /// Gets the reference to <see cref="Hsv"/>.
    /// </summary>
    /// <value>The type reference <c>core.types.hsv@1</c>.</value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static TypeRef HsvRef => ReferenceTo(Hsv);

    private static TypeRef ReferenceTo(DataTypeDef definition) => new(definition.Name, definition.Version.Major);
}
