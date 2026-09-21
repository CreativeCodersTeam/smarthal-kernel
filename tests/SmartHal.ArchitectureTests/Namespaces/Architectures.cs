using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using Assembly = System.Reflection.Assembly;

namespace SmartHal.ArchitectureTests.Namespaces;

/// <summary>
/// The assembly sets the namespace rules are evaluated against. Loading is deferred and cached,
/// because reading the assemblies is the expensive part of an ArchUnitNET run.
/// </summary>
internal static class Architectures
{
    private static readonly string[] ProductionAssemblyNames =
    [
        "SmartHal.Contracts",
        "SmartHal.Adapter.Sdk",
        "SmartHal.Automation.Sdk",
        "SmartHal.Core.Abstractions",
        "SmartHal.Core",
        "SmartHal.Server",
        "SmartHal.Cli"
    ];

    private static readonly Lazy<Architecture> LazyProduction = new(LoadProduction);

    private static readonly Lazy<Architecture> LazyProbe = new(LoadProbe);

    /// <summary>The seven production assemblies; every rule must hold here.</summary>
    public static Architecture Production => LazyProduction.Value;

    /// <summary>
    /// The test assembly itself. It carries the probe types that prove each rule actually fires.
    /// </summary>
    public static Architecture Probe => LazyProbe.Value;

    private static Architecture LoadProduction()
    {
        var assemblies = Array.ConvertAll(ProductionAssemblyNames, name => Assembly.Load(name));

        return new ArchLoader().LoadAssemblies(assemblies).Build();
    }

    private static Architecture LoadProbe()
    {
        return new ArchLoader().LoadAssemblies(typeof(Architectures).Assembly).Build();
    }
}
