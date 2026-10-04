using System.Text.RegularExpressions;

namespace SmartHal.Server.Configuration;

/// <summary>
/// Expands the user profile shortcut and the placeholders in a configured path.
/// </summary>
public static partial class PathExpansion
{
    /// <summary>
    /// Expands a leading <c>~</c> to the user profile and the placeholders <c>%NAME%</c>,
    /// <c>${NAME}</c> and <c>$NAME</c> to the environment variable of that name or, failing that, to
    /// the <see cref="Environment.SpecialFolder"/> of that name, such as <c>%CommonApplicationData%</c>.
    /// A placeholder that resolves to nothing stays as written.
    /// </summary>
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
