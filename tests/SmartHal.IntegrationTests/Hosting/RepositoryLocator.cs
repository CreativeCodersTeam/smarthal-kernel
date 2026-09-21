namespace SmartHal.IntegrationTests.Hosting;

/// <summary>
/// Locates the repository root by walking upwards from the test run's output directory until the
/// solution file is found.
/// </summary>
internal static class RepositoryLocator
{
    private const string SolutionFileName = "SmartHal.slnx";

    /// <summary>
    /// Returns the absolute path of the repository root.
    /// </summary>
    /// <returns>The directory that contains <c>SmartHal.slnx</c>.</returns>
    /// <exception cref="DirectoryNotFoundException">
    /// No solution file exists above <see cref="AppContext.BaseDirectory"/>.
    /// </exception>
    public static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"'{SolutionFileName}' was not found above '{AppContext.BaseDirectory}'.");
    }
}
