using AwesomeAssertions;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using SmartHal.Core.Abstractions.Validation;
using SmartHal.Core.Validation;
using Xunit;

namespace SmartHal.Core.UnitTests.Validation;

/// <summary>
/// Verifies that patterns are judged as ECMA-262: the constructs only ECMA-262 knows pass, malformed patterns and
/// out-of-range code points are reported, and escaped characters are never rewritten.
/// </summary>
public sealed class EcmaPatternTests
{
    private readonly ContractValidator _sut = new();

    [Theory]
    [InlineData("[]")]
    [InlineData("[^]")]
    [InlineData("a[]b")]
    [InlineData(@"\u{1F600}")]
    [InlineData(@"\u{41}")]
    [InlineData(@"x\u{10FFFF}")]
    [InlineData(@"[\u{1F600}a]")]
    [InlineData(@"\\u{41}")]
    [InlineData(@"\[]")]
    [InlineData(@"[\u{1F600}-\u{1F64F}]")]
    [InlineData(@"[^\u{1F600}-\u{1F64F}]")]
    [InlineData(@"[a\u{1F600}-\u{1F64F}z]")]
    [InlineData(@"[\u{1F600}-\u{1F600}]")]
    [InlineData(@"[\u{41}-\u{1F600}]")]
    [InlineData(@"[\uD83D\uDE00-\uD83D\uDE4F]")]
    [InlineData("[\U0001F600-\U0001F64F]")]
    [InlineData(@"[\u{1F600}]+")]
    [InlineData(@"\u{0000041}")]
    [InlineData(@"\u{00000000000000000000041}")]
    [InlineData(@"\u{1f600}")]
    [InlineData(@"\u{D7FF}")]
    [InlineData(@"\u{E000}")]
    [InlineData(@"\u{FFFF}")]
    [InlineData(@"\u{10000}")]
    [InlineData(@"\u{010FFF}")]
    [InlineData(@"\u{00FFFF}")]
    [InlineData(@"[\]]")]
    [InlineData(@"[\][]")]
    [InlineData(@"[a\]\u{41}]")]
    [InlineData("[^]]")]
    [InlineData(@"\\[]")]
    [InlineData(@"\\[^]")]
    [InlineData("[]*")]
    [InlineData("[]{2}")]
    [InlineData("[^]+")]
    public void Validate_PatternValidInEcmaScript_ReportsNoViolation(string pattern)
    {
        // Arrange
        var definition = Definition(pattern);

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(@"\u{110000}")]
    [InlineData(@"\u{}")]
    [InlineData(@"\u{1234567}")]
    [InlineData(@"\u{D800}")]
    [InlineData(@"\u{12")]
    [InlineData("(")]
    [InlineData("[a-")]
    [InlineData("a{2,1}")]
    [InlineData(@"[\u{1F64F}-\u{1F600}]")]
    [InlineData(@"[\u{1F600}-\u{41}]")]
    [InlineData(@"[\u{1F600}-a]")]
    [InlineData(@"[\uD83D\uDE4F-\uD83D\uDE00]")]
    [InlineData("[\U0001F64F-\U0001F600]")]
    [InlineData(@"\u{G}")]
    [InlineData(@"\u{+41}")]
    [InlineData(@"\u{ 41}")]
    [InlineData(@"\u{0x41}")]
    [InlineData(@"\u{DFFF}")]
    [InlineData(@"\u{FFFFFFFFFFFFFFFFFFFFFFFF}")]
    [InlineData(@"\u{")]
    [InlineData(@"a\")]
    [InlineData(@"[a\")]
    [InlineData(@"[\u{110000}]")]
    [InlineData(@"[a\u{}]")]
    [InlineData(@"[\u{D800}]")]
    public void Validate_MalformedPattern_ReportsInvalidPatternWithTheStableMessage(string pattern)
    {
        // Arrange
        var definition = Definition(pattern);

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.Should().ContainSingle().Which.Should().Be(
            new ValidationError(
                "dataType.pattern",
                ValidationCodes.InvalidPattern,
                $"The pattern '{pattern}' is not a valid regular expression."));
    }

    // The rewritten escapes are spelled as regular string literals with doubled backslashes.
    [Theory]
    [InlineData("[]", "(?!)")]
    [InlineData("[^]", @"[\s\S]")]
    [InlineData(@"\u{41}", "\\u0041")]
    [InlineData(@"\u{1F600}", "\\uD83D\\uDE00")]
    [InlineData(@"\\u{41}", @"\\u{41}")]
    [InlineData(@"\[]", @"\[]")]
    [InlineData("[a]]", "[a]]")]
    [InlineData(@"[[]", @"[[]")]
    [InlineData("[^]]", @"[\s\S]]")]
    [InlineData(@"\\[]", @"\\(?!)")]
    [InlineData(@"\\[^]", @"\\[\s\S]")]
    [InlineData(@"[\]]", @"[\]]")]
    [InlineData(@"[\][]", @"[\][]")]
    [InlineData(@"[a\]\u{41}]", @"[a\]\u0041]")]
    [InlineData(@"[\u{1F600}-\u{1F64F}]", "[a]")]
    [InlineData(@"[x\u{1F600}]", "[xa]")]
    [InlineData(@"\u{1f600}", @"\uD83D\uDE00")]
    [InlineData(@"\u{0000041}", @"\u0041")]
    [InlineData(@"\u{FFFF}", @"\uFFFF")]
    [InlineData(@"\u{10000}", @"\uD800\uDC00")]
    public void ToDotNet_EcmaScriptPattern_RewritesOnlyTheEcmaScriptOnlyConstructs(string pattern, string expected)
    {
        // Arrange

        // Act
        var rewritten = EcmaPattern.ToDotNet(pattern);

        // Assert
        rewritten.Should().Be(expected);
    }

    private static DataTypeDef Definition(string pattern) =>
        new("vendor.code", new TypeVersion(1, 0), new StringType(Pattern: pattern));
}
