using System.Text.RegularExpressions;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using SmartHal.Core.Abstractions.Validation;

namespace SmartHal.Core.Validation;

/// <summary>
/// Checks data types: struct depth (R1), data type constraints (R2) and, with a catalog, reusable type references
/// (R11) including the depth and the cycles they add.
/// </summary>
internal static class DataTypeRules
{
    private const int MaxStructDepth = 2;

    // A pattern is only compiled to prove it is a valid expression; it never runs against input.
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Checks a data type and everything nested in it.
    /// </summary>
    /// <param name="dataType">The data type to check.</param>
    /// <param name="path">The path of the data type.</param>
    /// <param name="context">The context the violations are reported to.</param>
    /// <param name="index">The catalog references are resolved in; <see langword="null"/> to skip reference checks.</param>
    public static void Check(DataType? dataType, string path, ValidationContext context, CatalogIndex? index) =>
        Check(dataType, path, context, index, depth: 0, owner: null);

    /// <summary>
    /// Checks the data type of a reusable data type definition and everything nested in it.
    /// </summary>
    /// <remarks>
    /// Inside a definition a reference can close a cycle back to the definition itself; that is reported as
    /// <see cref="ValidationCodes.RefCycle"/>.
    /// </remarks>
    /// <param name="definition">The definition whose data type is checked.</param>
    /// <param name="path">The path of the data type.</param>
    /// <param name="context">The context the violations are reported to.</param>
    /// <param name="index">The catalog references are resolved in; <see langword="null"/> to skip reference checks.</param>
    public static void CheckDefinition(DataTypeDef definition, string path, ValidationContext context, CatalogIndex? index)
    {
        Check(definition.DataType, path, context, index, depth: 0, owner: definition);
    }

    private static void Check(DataType? dataType, string path, ValidationContext context, CatalogIndex? index, int depth,
        DataTypeDef? owner)
    {
        switch (dataType)
        {
            case null:
                context.Add(path, ValidationCodes.NullEntry, "The data type is null.");
                break;
            case ObjectType objectType:
                CheckObject(objectType, path, context, index, depth, owner);
                break;
            case ArrayType arrayType:
                CheckLimit(arrayType.MaxItems, "maxItems", path, context);
                // An array does not add a struct level; only its items can.
                Check(arrayType.Items, ValidationContext.Member(path, "items"), context, index, depth, owner);
                break;
            case StringType stringType:
                CheckString(stringType, path, context);
                break;
            case EnumType enumType:
                CheckEnum(enumType, path, context);
                break;
            case NumericType numericType:
                CheckNumeric(numericType, path, context);
                break;
            case RefType refType:
                CheckReference(refType, path, context, index, depth, owner);
                break;
            default:
                // Boolean, timestamp and duration carry no constraint that could contradict itself.
                break;
        }
    }

    private static void CheckObject(
        ObjectType objectType,
        string path,
        ValidationContext context,
        CatalogIndex? index,
        int depth,
        DataTypeDef? owner)
    {
        var level = depth + 1;

        if (level > MaxStructDepth)
        {
            context.Add(
                path,
                ValidationCodes.StructTooDeep,
                ValidationContext.Invariant($"The struct is nested {level} levels deep; at most {MaxStructDepth} levels are allowed."));

            return;
        }

        var fieldsPath = ValidationContext.Member(path, "fields");
        var fields = context.Entries(objectType.Fields, fieldsPath, required: true);
        var declared = ValidationContext.KeysOf(objectType.Fields);
        var requiredPath = ValidationContext.Member(path, "required");

        foreach (var (name, i) in context.Entries(objectType.Required, requiredPath, required: false))
        {
            // A field whose type is null is still declared; without any fields there is nothing to compare with.
            if (declared is not null && !declared.Contains(name))
            {
                context.Add(
                    ValidationContext.Index(requiredPath, i),
                    ValidationCodes.UnknownField,
                    $"The required field '{name}' is not a field of the struct.");
            }
        }

        foreach (var (field, name) in fields)
        {
            Check(field, ValidationContext.Member(fieldsPath, name), context, index, level, owner);
        }
    }

    private static void CheckString(StringType stringType, string path, ValidationContext context)
    {
        CheckLimit(stringType.MaxLength, "maxLength", path, context);

        if (stringType.Pattern is not { } pattern)
        {
            return;
        }

        if (!IsWellFormed(pattern))
        {
            // The message of the parser is localized and version-dependent; the violation carries its own.
            context.Add(
                ValidationContext.Member(path, "pattern"),
                ValidationCodes.InvalidPattern,
                $"The pattern '{pattern}' is not a valid regular expression.");
        }
    }

    // JSON Schema patterns follow ECMA-262: the constructs only ECMA-262 knows are rewritten first, then the pattern
    // is compiled in the ECMAScript mode of .NET.
    private static bool IsWellFormed(string pattern)
    {
        if (EcmaPattern.ToDotNet(pattern) is not { } dotNetPattern)
        {
            return false;
        }

        try
        {
            _ = new Regex(dotNetPattern, RegexOptions.ECMAScript, PatternTimeout);

            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void CheckLimit(int? limit, string member, string path, ValidationContext context)
    {
        if (limit is < 0)
        {
            context.Add(ValidationContext.Member(path, member), ValidationCodes.InvalidRange,
                ValidationContext.Invariant($"The {member} {limit} is negative."));
        }
    }

    private static void CheckEnum(EnumType enumType, string path, ValidationContext context)
    {
        var valuesPath = ValidationContext.Member(path, "values");
        var values = context.Entries(enumType.Values, valuesPath, required: true);

        if (!ValidationContext.IsNull(enumType.Values) && enumType.Values.Count == 0)
        {
            context.Add(valuesPath, ValidationCodes.EmptyEnum, "The enum defines no values.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (value, i) in values)
        {
            if (!seen.Add(value))
            {
                context.Add(
                    ValidationContext.Index(valuesPath, i),
                    ValidationCodes.DuplicateEnumValue,
                    $"The enum value '{value}' occurs more than once.");
            }
        }
    }

    private static void CheckNumeric(NumericType numericType, string path, ValidationContext context)
    {
        // Both bounds are checked, so both are reported when both are not finite.
        var finiteMinimum = CheckFinite(numericType.Minimum, "minimum", path, context);
        var finiteMaximum = CheckFinite(numericType.Maximum, "maximum", path, context);

        if (finiteMinimum && finiteMaximum && numericType is { Minimum: { } minimum, Maximum: { } maximum } && minimum > maximum)
        {
            context.Add(
                ValidationContext.Member(path, "minimum"),
                ValidationCodes.InvalidRange,
                ValidationContext.Invariant($"The minimum {minimum} is greater than the maximum {maximum}."));
        }

        if (numericType.Step is { } step && (!double.IsFinite(step) || step <= 0))
        {
            context.Add(
                ValidationContext.Member(path, "step"),
                ValidationCodes.InvalidStep,
                ValidationContext.Invariant($"The step {step} is not a finite number greater than zero."));
        }
    }

    // Reports a bound that is NaN or infinite; returns whether the bound can take part in the range comparison.
    private static bool CheckFinite(double? bound, string member, string path, ValidationContext context)
    {
        if (bound is not { } value || double.IsFinite(value))
        {
            return true;
        }

        context.Add(ValidationContext.Member(path, member), ValidationCodes.InvalidRange,
            ValidationContext.Invariant($"The {member} {value} is not a finite number."));

        return false;
    }

    private static void CheckReference(
        RefType refType,
        string path,
        ValidationContext context,
        CatalogIndex? index,
        int depth,
        DataTypeDef? owner)
    {
        // A reference read from JSON can arrive without its type name; it refers to nothing and is reported once,
        // with or without a catalog.
        if (ValidationContext.IsNull(refType.Ref.Name))
        {
            context.Add(ValidationContext.Member(path, "ref"), ValidationCodes.NullEntry, "The reference names no data type.");

            return;
        }

        if (index is null)
        {
            return;
        }

        if (index.FindDataType(refType.Ref) is null)
        {
            context.Add(
                ValidationContext.Member(path, "ref"),
                ValidationCodes.UnresolvedType,
                $"The data type '{refType.Ref}' is not in the type catalog.");

            return;
        }

        // The referenced type is checked on its own as a catalog entry; here only what the reference adds counts:
        // whether it closes a cycle back to the definition it is part of, and the struct levels it brings to this
        // place. A reference that merely points into a cycle elsewhere is not a cycle of its own.
        var references = index.References;

        if (owner is not null && references.ClosesCycle(owner, refType.Ref))
        {
            context.Add(path, ValidationCodes.RefCycle, $"The reference to '{refType.Ref}' leads back to a data type it is part of.");

            return;
        }

        var levels = references.Levels(refType.Ref);

        if (depth + levels > MaxStructDepth)
        {
            context.Add(
                path,
                ValidationCodes.StructTooDeep,
                ValidationContext.Invariant(
                        $"Through the reference to '{refType.Ref}' the struct is nested {depth + levels} levels deep; at most ")
                    + ValidationContext.Invariant($"{MaxStructDepth} levels are allowed."));
        }
    }
}
