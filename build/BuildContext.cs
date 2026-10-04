using Cake.Common.Build;
using Cake.Core;
using Cake.Core.IO;
using CreativeCoders.CakeBuild;
using CreativeCoders.CakeBuild.Tasks.Defaults;
using CreativeCoders.CakeBuild.Tasks.Templates.Settings;

namespace Build;

// Package metadata (project url, license) comes from Directory.Build.props, so the pack settings keep their defaults.
public class BuildContext(ICakeContext context)
    : CakeBuildContext(context), IDefaultTaskSettings, ICreateDistPackagesTaskSettings, ICreateGitHubReleaseTaskSettings
{
    public bool UseMicrosoftTestingPlatform => true;

    public string NuGetFeedUrl => this.GitHubActions().Environment.Workflow.Workflow == "release"
        ? "nuget.org"
        : "https://nuget.pkg.github.com/CreativeCodersTeam/index.json";

    public bool SkipPush => this.BuildSystem().IsPullRequest ||
                            this.BuildSystem().IsLocalBuild ||
                            this.GitHubActions().Environment.Runner.OS != "Linux";

    public DirectoryPath PublishOutputDir => ArtifactsDir.Combine("published");

    public IEnumerable<PublishingItem> PublishingItems =>
    [
        new PublishingItem(
            RootDir.CombineWithFilePath("src/SmartHal.Server/SmartHal.Server.csproj"),
            PublishOutputDir.Combine("server")),
        new PublishingItem(
            RootDir.CombineWithFilePath("src/SmartHal.Cli/SmartHal.Cli.csproj"),
            PublishOutputDir.Combine("cli"))
    ];

    private const string ServerDistPackageName = "SmartHal.Server";

    private const string CliDistPackageName = "SmartHal.Cli";

    public IEnumerable<DistPackage> DistPackages =>
    [
        new DistPackage(ServerDistPackageName, PublishOutputDir.Combine("server")),
        new DistPackage(CliDistPackageName, PublishOutputDir.Combine("cli"))
    ];

    public string ReleaseName => $"v{Version.FullSemVer}";

    public string ReleaseVersion => $"v{Version.FullSemVer}";

    public string ReleaseBody => "SmartHal Kernel Release";

    public bool IsPreRelease => !string.IsNullOrWhiteSpace(Version.PreReleaseTag);

    public IEnumerable<GitHubReleaseAsset> ReleaseAssets =>
    [
        DistPackageAsset(ServerDistPackageName),
        DistPackageAsset(CliDistPackageName)
    ];

    private GitHubReleaseFileAsset DistPackageAsset(string distPackageName)
    {
        return new GitHubReleaseFileAsset(
            GetRequiredSettings<ICreateDistPackagesTaskSettings>().DistOutputPath
                .CombineWithFilePath(distPackageName + ".tar.gz").FullPath, null);
    }
}
