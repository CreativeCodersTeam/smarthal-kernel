using System.Text.Json;
using System.Xml.Linq;
using AwesomeAssertions;
using Xunit;

namespace SmartHal.ArchitectureTests.Solution;

/// <summary>
/// Verifies the build, package and analyzer configuration of the solution against the specification
/// (AC-33, FR-11, FR-12, FR-38, FR-41, FR-44 through FR-50, FR-62, FR-65).
/// </summary>
public sealed class ProjectPropertiesTests
{
    private static readonly string[] PackableProjectNames =
    [
        "SmartHal.Contracts",
        "SmartHal.Adapter.Sdk",
        "SmartHal.Automation.Sdk"
    ];

    private static readonly string[] ExecutableProjectNames = ["SmartHal.Server", "SmartHal.Cli"];

    private static readonly string[] MandatedTestPackages =
    [
        "xunit.v3",
        "xunit.runner.visualstudio",
        "Microsoft.NET.Test.Sdk",
        "FakeItEasy",
        "AwesomeAssertions",
        "TngTech.ArchUnitNET",
        "TngTech.ArchUnitNET.xUnitV3",
        "Microsoft.Extensions.Diagnostics.Testing",
        "Microsoft.Testing.Extensions.TrxReport"
    ];

    private static readonly string[] EmptyUnitTestProjectNames =
    [
        "SmartHal.Contracts.UnitTests",
        "SmartHal.Adapter.Sdk.UnitTests",
        "SmartHal.Automation.Sdk.UnitTests",
        "SmartHal.Core.Abstractions.UnitTests",
        "SmartHal.Core.UnitTests",
        "SmartHal.Cli.UnitTests"
    ];

    private static readonly string[] ForbiddenSeverityProperties =
        ["NoWarn", "WarningsNotAsErrors", "WarningLevel"];

    private static readonly string[] SourceRootFolders = ["src", "tests"];

    private static readonly string[] MandatedTestStackOfTestsBuildProps =
    [
        "xunit.v3",
        "xunit.runner.visualstudio",
        "Microsoft.NET.Test.Sdk",
        "FakeItEasy",
        "AwesomeAssertions"
    ];

    private static readonly string[] MandatedGlobalAnalyzers =
        ["Roslynator.Analyzers", "SonarAnalyzer.CSharp"];

    private static readonly string[] HandMaintainedVersionProperties =
        ["Version", "VersionPrefix", "AssemblyVersion"];

    [Fact]
    public void TestProjects_PackageReferences_UseOnlyTheMandatedTestStack()
    {
        // Arrange
        var testBuildFiles = TestProjectFiles().Append(TestsBuildProps()).ToArray();

        // Act
        var referencedPackages = testBuildFiles
            .SelectMany(file => PackageReferenceNames(file.Document))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Assert
        referencedPackages.Should().NotBeEmpty();
        referencedPackages.Should().BeSubsetOf(MandatedTestPackages);
    }

    [Fact]
    public void ProjectFiles_IsPackable_TrueOnlyForContractsAndBothSdks()
    {
        // Arrange & Act
        var packableProjects = ProjectFiles()
            .Where(file => string.Equals(PropertyValue(file.Document, "IsPackable"), "true", StringComparison.OrdinalIgnoreCase))
            .Select(file => file.Name)
            .ToArray();

        // Assert
        packableProjects.Should().BeEquivalentTo(PackableProjectNames);
        PropertyValue(RootBuildProps().Document, "IsPackable").Should().Be("false");
    }

    [Fact]
    public void ProjectFiles_OutputType_ExeOnlyForServerAndCli()
    {
        // Arrange & Act
        var projectsWithOutputType = ProjectFiles()
            .Where(file => PropertyValue(file.Document, "OutputType") is not null)
            .Select(file => new { file.Name, OutputType = PropertyValue(file.Document, "OutputType") })
            .ToArray();

        // Assert
        projectsWithOutputType.Select(project => project.Name).Should().BeEquivalentTo(ExecutableProjectNames);
        projectsWithOutputType.Should().OnlyContain(project => project.OutputType == "Exe");
    }

    [Fact]
    public void PackageVersions_Declared_ContainNoOpenTelemetryPackage()
    {
        // Arrange
        var declaredPackages = DeclaredPackageNames();
        var referencedPackages = AllBuildFiles()
            .SelectMany(file => PackageReferenceNames(file.Document))
            .ToArray();

        // Act
        var openTelemetryPackages = declaredPackages
            .Concat(referencedPackages)
            .Where(package => package.Contains("OpenTelemetry", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // Assert
        declaredPackages.Should().NotBeEmpty();
        openTelemetryPackages.Should().BeEmpty("FR-38 forbids a wired-up OpenTelemetry SDK");
    }

    [Fact]
    public void ProjectFiles_Sdk_IsMicrosoftNetSdkNotWeb()
    {
        // Arrange & Act
        var declaredSdks = ProjectFiles()
            .Select(file => new { file.Name, Sdk = (string?)file.Document.Root?.Attribute("Sdk") })
            .ToArray();

        // Assert
        declaredSdks.Should().HaveCount(16);
        declaredSdks.Should().OnlyContain(project => project.Sdk == "Microsoft.NET.Sdk");
    }

    [Fact]
    public void DirectoryBuildProps_TargetFramework_IsNet10AndNoProjectOverridesIt()
    {
        // Arrange & Act
        var overridingProjects = ProjectFiles()
            .Where(file => PropertyValue(file.Document, "TargetFramework") is not null
                || PropertyValue(file.Document, "TargetFrameworks") is not null)
            .Select(file => file.Name)
            .ToArray();

        // Assert
        PropertyValue(RootBuildProps().Document, "TargetFramework").Should().Be("net10.0");
        overridingProjects.Should().BeEmpty();
    }

    [Fact]
    public void GlobalJson_Parsed_BindsSdk10WithLatestFeatureRollForward()
    {
        // Arrange
        var globalJsonPath = Path.Combine(RepositoryLocator.FindRoot(), "global.json");

        // Act
        using var document = JsonDocument.Parse(File.ReadAllText(globalJsonPath));
        var sdk = document.RootElement.GetProperty("sdk");

        // Assert
        sdk.GetProperty("version").GetString().Should().StartWith("10.0.");
        sdk.GetProperty("rollForward").GetString().Should().Be("latestFeature");
    }

    [Theory]
    [InlineData("TargetFramework", "net10.0")]
    [InlineData("Nullable", "enable")]
    [InlineData("ImplicitUsings", "enable")]
    [InlineData("LangVersion", "latest")]
    [InlineData("TreatWarningsAsErrors", "true")]
    [InlineData("EnforceCodeStyleInBuild", "true")]
    [InlineData("AnalysisLevel", "latest-Recommended")]
    [InlineData("Deterministic", "true")]
    public void DirectoryBuildProps_Parsed_SetsAllRequiredProperties(string propertyName, string expectedValue)
    {
        // Arrange & Act
        var actualValue = PropertyValue(RootBuildProps().Document, propertyName);

        // Assert
        actualValue.Should().Be(expectedValue);
    }

    [Fact]
    public void ProjectFiles_PackageReferences_CarryNoVersionAttribute()
    {
        // Arrange & Act
        var offendingReferences = AllBuildFiles()
            .SelectMany(file => file.Document
                .Descendants("PackageReference")
                .Where(reference => reference.Attribute("Version") is not null
                    || reference.Attribute("VersionOverride") is not null)
                .Select(reference => $"{file.Name}: {(string?)reference.Attribute("Include")}"))
            .ToArray();

        // Assert
        PropertyValue(PackagesProps().Document, "ManagePackageVersionsCentrally").Should().Be("true");
        offendingReferences.Should().BeEmpty("FR-47 requires central package management with no version in the project file");
    }

    [Fact]
    public void TestsDirectoryBuildProps_Parsed_SetsIsPackableFalseAndTestStack()
    {
        // Arrange
        var testsBuildProps = TestsBuildProps();

        // Act
        var testStack = PackageReferenceNames(testsBuildProps.Document);

        // Assert
        PropertyValue(testsBuildProps.Document, "IsPackable").Should().Be("false");
        testStack.Should().Contain(MandatedTestStackOfTestsBuildProps);
    }

    [Fact]
    public void DirectoryPackagesProps_Parsed_ReferencesRoslynatorSonarAndAnalysisLevelLatestRecommended()
    {
        // Arrange & Act
        var globalAnalyzers = PackagesProps().Document
            .Descendants("GlobalPackageReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .OfType<string>()
            .ToArray();

        // Assert
        globalAnalyzers.Should().Contain(MandatedGlobalAnalyzers);
        PropertyValue(RootBuildProps().Document, "AnalysisLevel").Should().Be("latest-Recommended");
    }

    [Fact]
    public void PropsAndProjectFiles_Parsed_ContainNoHandMaintainedVersion()
    {
        // Arrange
        var buildFiles = AllBuildFiles();

        // Act
        var handMaintainedVersions = buildFiles
            .SelectMany(file => HandMaintainedVersionProperties
                .Where(property => PropertyValue(file.Document, property) is not null)
                .Select(property => $"{file.Name}: {property}"))
            .ToArray();
        var globalPackages = PackagesProps().Document
            .Descendants("GlobalPackageReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .OfType<string>()
            .ToArray();

        // Assert
        handMaintainedVersions.Should().BeEmpty("per FR-67 GitVersion derives the version from the git history");
        globalPackages.Should().Contain("GitVersion.MsBuild");
    }

    [Fact]
    public void DirectoryBuildProps_Parsed_EnablesSourceLinkDeterministicAndSnupkg()
    {
        // Arrange
        var buildProps = RootBuildProps().Document;

        // Act
        var continuousIntegrationBuild = buildProps.Descendants("ContinuousIntegrationBuild").FirstOrDefault();
        var continuousIntegrationCondition = string.Concat(
            (string?)continuousIntegrationBuild?.Attribute("Condition") ?? string.Empty,
            (string?)continuousIntegrationBuild?.Parent?.Attribute("Condition") ?? string.Empty);

        // Assert
        PropertyValue(buildProps, "EmbedUntrackedSources").Should().Be("true");
        PropertyValue(buildProps, "PublishRepositoryUrl").Should().Be("true");
        PropertyValue(buildProps, "IncludeSymbols").Should().Be("true");
        PropertyValue(buildProps, "SymbolPackageFormat").Should().Be("snupkg");
        continuousIntegrationBuild.Should().NotBeNull("FR-72 requires a deterministic CI build");
        continuousIntegrationBuild.Value.Trim().Should().Be("true");
        continuousIntegrationCondition.Should().Contain("GITHUB_ACTIONS");
    }

    [Fact]
    public void ProjectFiles_Parsed_ContainNoSeverityOrNoWarnSettings()
    {
        // Arrange
        var buildFiles = AllBuildFiles();

        // Act
        var offendingProperties = buildFiles
            .SelectMany(file => ForbiddenSeverityProperties
                .Where(property => PropertyValue(file.Document, property) is not null)
                .Select(property => $"{file.Name}: {property}"))
            .ToArray();
        var offendingDiagnostics = buildFiles
            .Where(file => File.ReadAllText(file.FullPath).Contains("dotnet_diagnostic", StringComparison.OrdinalIgnoreCase))
            .Select(file => file.Name)
            .ToArray();

        // Assert
        offendingProperties.Should().BeEmpty("per FR-50 severities belong exclusively in .editorconfig");
        offendingDiagnostics.Should().BeEmpty("per FR-50 severities belong exclusively in .editorconfig");
    }

    [Fact]
    public void EmptyUnitTestProjects_Inspected_ContainNoSourceFiles()
    {
        // Arrange
        var testsRoot = Path.Combine(RepositoryLocator.FindRoot(), "tests");

        // Act
        var projectsWithSourceFiles = EmptyUnitTestProjectNames
            .Select(name => new { Name = name, Files = SourceFilesOf(Path.Combine(testsRoot, name)) })
            .Where(project => project.Files.Length > 0)
            .Select(project => $"{project.Name}: {string.Join(", ", project.Files)}")
            .ToArray();

        // Assert
        projectsWithSourceFiles.Should().BeEmpty("FR-65 forbids placeholder tests in the empty unit test projects");
    }

    private static string[] SourceFilesOf(string projectDirectory)
    {
        if (!Directory.Exists(projectDirectory))
        {
            return ["<project directory is missing>"];
        }

        return Directory
            .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(projectDirectory, file))
            .Where(relative => !relative.StartsWith("obj", StringComparison.OrdinalIgnoreCase)
                && !relative.StartsWith("bin", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static string? PropertyValue(XDocument document, string propertyName)
    {
        return document
            .Descendants(propertyName)
            .Select(element => element.Value.Trim())
            .FirstOrDefault();
    }

    private static string[] PackageReferenceNames(XDocument document)
    {
        return document
            .Descendants("PackageReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .OfType<string>()
            .ToArray();
    }

    private static string[] DeclaredPackageNames()
    {
        return PackagesProps().Document
            .Descendants()
            .Where(element => element.Name.LocalName is "PackageVersion" or "GlobalPackageReference")
            .Select(element => (string?)element.Attribute("Include"))
            .OfType<string>()
            .ToArray();
    }

    private static MsBuildFile[] AllBuildFiles()
    {
        return
        [
            .. ProjectFiles(),
            RootBuildProps(),
            TestsBuildProps(),
            PackagesProps()
        ];
    }

    private static MsBuildFile[] ProjectFiles()
    {
        var root = RepositoryLocator.FindRoot();

        return SourceRootFolders
            .Select(folder => Path.Combine(root, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.csproj", SearchOption.AllDirectories))
            .Select(Load)
            .ToArray();
    }

    private static MsBuildFile[] TestProjectFiles()
    {
        var testsRoot = Path.Combine(RepositoryLocator.FindRoot(), "tests") + Path.DirectorySeparatorChar;

        return ProjectFiles()
            .Where(file => file.FullPath.StartsWith(testsRoot, StringComparison.Ordinal))
            .ToArray();
    }

    private static MsBuildFile RootBuildProps()
    {
        return Load(Path.Combine(RepositoryLocator.FindRoot(), "Directory.Build.props"));
    }

    private static MsBuildFile TestsBuildProps()
    {
        return Load(Path.Combine(RepositoryLocator.FindRoot(), "tests", "Directory.Build.props"));
    }

    private static MsBuildFile PackagesProps()
    {
        return Load(Path.Combine(RepositoryLocator.FindRoot(), "Directory.Packages.props"));
    }

    private static MsBuildFile Load(string path)
    {
        var name = path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(path)
            : Path.GetFileName(path);

        return new MsBuildFile(name, path, XDocument.Load(path));
    }

    private sealed record MsBuildFile(string Name, string FullPath, XDocument Document);
}
