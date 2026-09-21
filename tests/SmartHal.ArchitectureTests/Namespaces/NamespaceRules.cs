using ArchUnitNET.Fluent;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace SmartHal.ArchitectureTests.Namespaces;

/// <summary>
/// The namespace bans of the specification (section 6.2). Every rule is a plain
/// <see cref="IArchRule"/>, so the same rule can be evaluated against the production assemblies and
/// against the probe assembly that proves it fires.
/// </summary>
/// <remarks>
/// Every rule ends in <c>WithoutRequiringPositiveResults()</c>. ArchUnitNET otherwise reports a
/// failure as soon as no type matches the filter at all, and the production projects of this slice
/// are still empty: an absent type cannot break a namespace ban.
/// </remarks>
internal static class NamespaceRules
{
    // The kernel and the published projects: Contracts, both SDKs, Core.Abstractions and Core.
    // Core.Abstractions is covered by the Core branch because its namespace starts with it.
    private const string KernelAndPublishedNamespaces =
        @"^SmartHal\.(Contracts|Adapter\.Sdk|Automation\.Sdk|Core)($|\.)";

    private const string AnyProductionNamespace = @"^SmartHal($|\.)";

    private const string CompositionRootNamespace = @"^SmartHal\.Server\.Composition($|\.)";

    private const string MqttNamespaces = "(?i)mqtt";

    private const string HomeAssistantNamespaces = @"(?i)homeassistant|(?i)\bhass";

    private const string SerilogNamespaces = @"^Serilog($|\.)";

    /// <summary>
    /// No type in the kernel or in a published project touches an MQTT namespace
    /// (ADR-0002, FR-58).
    /// </summary>
    public static IArchRule NoMqttInKernelAndPublished { get; } =
        Types()
            .That().ResideInNamespaceMatching(KernelAndPublishedNamespaces)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(MqttNamespaces)
            .Because("ADR-0002 keeps MQTT out of the kernel and the published projects (FR-58)")
            .WithoutRequiringPositiveResults();

    /// <summary>
    /// No type in the kernel or in a published project touches a Home Assistant namespace
    /// (ADR-0003, FR-59).
    /// </summary>
    public static IArchRule NoHomeAssistantInKernelAndPublished { get; } =
        Types()
            .That().ResideInNamespaceMatching(KernelAndPublishedNamespaces)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(HomeAssistantNamespaces)
            .Because("ADR-0003 keeps Home Assistant out of the kernel and the published projects (FR-59)")
            .WithoutRequiringPositiveResults();

    /// <summary>
    /// Serilog types appear exclusively in the composition root of <c>SmartHal.Server</c>; every
    /// other production type logs through <c>ILogger&lt;T&gt;</c> (FR-27, FR-60).
    /// </summary>
    public static IArchRule SerilogOnlyInCompositionRoot { get; } =
        Types()
            .That().ResideInNamespaceMatching(AnyProductionNamespace)
            .And().DoNotResideInNamespaceMatching(CompositionRootNamespace)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(SerilogNamespaces)
            .Because("FR-27 confines Serilog to the composition root of SmartHal.Server (FR-60)")
            .WithoutRequiringPositiveResults();
}
