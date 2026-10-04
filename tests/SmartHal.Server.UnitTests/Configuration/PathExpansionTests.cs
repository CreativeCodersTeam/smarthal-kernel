using System.Globalization;
using AwesomeAssertions;
using SmartHal.Server.Configuration;
using Xunit;

namespace SmartHal.Server.UnitTests.Configuration;

/// <summary>
/// Verifies the expansion of the user profile shortcut and of the placeholders in a configured path.
/// </summary>
public sealed class PathExpansionTests : IDisposable
{
    // Unique per instance, so the process-wide environment never collides with a parallel test.
    private readonly string _variable = $"SMARTHAL_TEST_{Guid.NewGuid():N}";

    private readonly List<string> _setVariables = [];

    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    [Theory]
    [InlineData("~", "")]
    [InlineData("~/data", "/data")]
    [InlineData(@"~\data", @"\data")]
    public void Expand_LeadingTilde_ReplacesItWithUserProfile(string path, string rest)
    {
        // Arrange & Act
        var result = PathExpansion.Expand(path);

        // Assert
        result.Should().Be(Home + rest);
    }

    [Theory]
    [InlineData("%{0}%/data")]
    [InlineData("${{{0}}}/data")]
    [InlineData("${0}/data")]
    public void Expand_PlaceholderOfSetVariable_ReplacesItWithValue(string format)
    {
        // Arrange
        SetVariable(_variable, "/env");
        var path = string.Format(CultureInfo.InvariantCulture, format, _variable);

        // Act
        var result = PathExpansion.Expand(path);

        // Assert
        result.Should().Be("/env/data");
    }

    [Fact]
    public void Expand_SeveralPlaceholders_ReplacesEachOfThem()
    {
        // Arrange
        SetVariable(_variable, "/env");
        var path = $"%{_variable}%/${{{_variable}}}/${_variable}";

        // Act
        var result = PathExpansion.Expand(path);

        // Assert
        result.Should().Be("/env//env//env");
    }

    [Fact]
    public void Expand_DollarNameFollowedByText_TakesTheLongestIdentifierAsName()
    {
        // Arrange
        SetVariable(_variable, "/env");
        var path = $"${_variable}_suffix/data";

        // Act
        var result = PathExpansion.Expand(path);

        // Assert
        result.Should().Be(path, "the name runs up to the first character outside [A-Za-z0-9_]");
    }

    [Fact]
    public void Expand_TildeAndPlaceholder_ExpandsBoth()
    {
        // Arrange
        SetVariable(_variable, "data");
        var path = $"~/%{_variable}%";

        // Act
        var result = PathExpansion.Expand(path);

        // Assert
        result.Should().Be(Home + "/data");
    }

    [Theory]
    [InlineData("%commonapplicationdata%/data")]
    [InlineData("%CommonApplicationData%/data")]
    [InlineData("${CommonApplicationData}/data")]
    [InlineData("$CommonApplicationData/data")]
    public void Expand_SpecialFolderName_ReplacesItWithFolderPathIgnoringCase(string path)
    {
        // Arrange
        var commonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        // Act
        var result = PathExpansion.Expand(path);

        // Assert
        result.Should().Be(commonData + "/data");
    }

    [Fact]
    public void Expand_VariableNamedLikeSpecialFolder_PrefersVariable()
    {
        // Arrange
        SetVariable(nameof(Environment.SpecialFolder.ApplicationData), "/env");

        // Act
        var result = PathExpansion.Expand("%ApplicationData%/data");

        // Assert
        result.Should().Be("/env/data");
    }

    [Theory]
    [InlineData("%5%/data")]
    [InlineData("%Desktop,Fonts%/data")]
    public void Expand_NumberOrFlagListInsteadOfFolderName_StaysAsWritten(string path)
    {
        // Arrange & Act
        var result = PathExpansion.Expand(path);

        // Assert
        result.Should().Be(path, "only an exact special folder name resolves");
    }

    [Theory]
    [InlineData("")]
    [InlineData("/var/lib/smarthal")]
    [InlineData("~data")]
    [InlineData("data/~")]
    [InlineData("data/~/more")]
    [InlineData("%SMARTHAL_UNSET_VARIABLE%/data")]
    [InlineData("${SMARTHAL_UNSET_VARIABLE}/data")]
    [InlineData("$SMARTHAL_UNSET_VARIABLE/data")]
    [InlineData("100%/data")]
    [InlineData("%%/data")]
    [InlineData("$/data")]
    [InlineData("$1/data")]
    [InlineData("${}/data")]
    public void Expand_NothingToExpandOrUnresolvable_StaysAsWritten(string path)
    {
        // Arrange & Act
        var result = PathExpansion.Expand(path);

        // Assert
        result.Should().Be(path);
    }

    [Fact]
    public void Expand_SpecialFolderWithoutPathOnThisPlatform_StaysAsWritten()
    {
        // Arrange
        // Which folders have no path depends on the platform, so the folder is looked up at run time.
        var emptyFolder = Enum.GetNames<Environment.SpecialFolder>()
            .FirstOrDefault(name => Environment.GetEnvironmentVariable(name) is null
                && Environment.GetFolderPath(Enum.Parse<Environment.SpecialFolder>(name)).Length == 0);
        Assert.SkipWhen(emptyFolder is null, "every special folder has a path on this platform");
        var path = $"%{emptyFolder}%/data";

        // Act
        var result = PathExpansion.Expand(path);

        // Assert
        result.Should().Be(path);
    }

    public void Dispose()
    {
        foreach (var name in _setVariables)
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    private void SetVariable(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        _setVariables.Add(name);
    }
}
