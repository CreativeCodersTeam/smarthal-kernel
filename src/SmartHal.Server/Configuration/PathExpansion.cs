using System.Text.RegularExpressions;

namespace SmartHal.Server.Configuration;

/// <summary>
/// Makes configured paths portable by expanding <c>~</c> and environment placeholders.
/// </summary>
public static partial class PathExpansion
{
    /// <summary>
    /// Expands <c>~</c> and the placeholders <c>%NAME%</c>, <c>${NAME}</c> and <c>$NAME</c> in a path.
    /// </summary>
    /// <remarks>
    /// A placeholder resolves to an environment variable or, failing that, to an
    /// <see cref="Environment.SpecialFolder"/>; an unresolved placeholder stays as written.
    /// </remarks>
    /// <param name="path">The path as configured.</param>
    /// <returns>The expanded path.</returns>
    public static string Expand(string path)
    {
        if (path == "~" || path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith(@"~\", StringComparison.Ordinal))
        {
            path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..];
        }

        return Placeholder().Replace(path, match =>
        {
            var name = match.Groups["name"].Value;
            var value = Environment.GetEnvironmentVariable(name);

            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            // Matched by name rather than Enum.TryParse, which would also accept numbers and flag lists.
            var folder = Enum.GetNames<Environment.SpecialFolder>()
                .FirstOrDefault(folderName => folderName.Equals(name, StringComparison.OrdinalIgnoreCase));
            var folderPath = folder is null ? "" : Environment.GetFolderPath(Enum.Parse<Environment.SpecialFolder>(folder));

            return folderPath.Length > 0 ? folderPath : match.Value;
        });
    }

    [GeneratedRegex(
        @"%(?<name>[^%]+)%|\$\{(?<name>[^}]+)\}|\$(?<name>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();
}
