using CreativeCoders.CakeBuild;

namespace Build;

internal static class Program
{
    internal static int Main(string[] args)
    {
        return CakeHostBuilder.Create()
            .UseBuildContext<BuildContext>()
            .AddDefaultTasks()
            .AddBuildServerIntegration()
            .InstallTools(
                // Same version as the GitVersion.MsBuild package, so Cake and MSBuild compute the same version.
                new DotNetToolInstallation("GitVersion.Tool", "6.8.2"),
                new DotNetToolInstallation("dotnet-reportgenerator-globaltool", "5.5.11"))
            .Build()
            .Run(args);
    }
}
