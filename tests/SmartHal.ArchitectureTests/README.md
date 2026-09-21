# SmartHal.ArchitectureTests

The architecture tests are a **growing deliverable** (FR-61). Slice 0 lays down the repository
layout, the reference topology and the namespace bans. Every following slice adds the boundaries it
draws, and its own spec names them explicitly.

## What is checked here

| Area | Folder | Checked against |
| --- | --- | --- |
| Repository layout and build configuration | `Solution/` | the repository tree and the MSBuild files |
| Project reference topology | `Topology/` | the `.csproj` files, read into a `ProjectGraph` |
| Namespace bans | `Namespaces/` | the compiled assemblies, via ArchUnitNET |

Two tools, because they answer different questions (C-5):

- **Project references** are read from the project files. An unused `ProjectReference` leaves no
  trace in the compiled assembly, so an assembly-based check would miss it.
- **Namespace bans** are checked with ArchUnitNET (`TngTech.ArchUnitNET` +
  `TngTech.ArchUnitNET.xUnitV3`) against the assemblies, because that is where type usage lives.

## Architectures

`Namespaces/Architectures.cs` offers the two assembly sets every namespace rule is evaluated
against:

- `Architectures.Production` — the seven production assemblies. Every rule must hold here.
- `Architectures.Probe` — this test assembly. It carries the probe types that prove a rule fires.

## How a slice adds a rule

Adding a rule is always the same three steps, and none of them may be skipped:

1. **Write the rule down as data or as an `IArchRule`.**
   - A reference rule goes into `Topology/ReferenceTopology.cs` (the table) and, if it is a new
     kind of rule, as a pure function into `Topology/TopologyRules.cs`. A rule function takes a
     `ProjectGraph` and a `ReferenceTopology` and returns `TopologyViolation`s — it never reads the
     file system itself.
   - A namespace rule goes into `Namespaces/NamespaceRules.cs` as a `static IArchRule` property.
     End the fluent chain with `.Because("<reason> (<requirement id>)")` so a failure explains
     itself, and with `.WithoutRequiringPositiveResults()` so an empty filter set is not reported as
     a violation.

2. **Add a probe that breaks the rule.**
   - For a namespace rule: a type under `Probes/`, in a namespace that makes it look like
     production code (for example `SmartHal.Core.Probes`), touching a *faked* forbidden namespace
     that is also declared under `Probes/`. Never add a real package reference for the forbidden
     library — the point is to prove the rule without pulling the dependency in.
   - For a reference rule: no probe type is needed. Build a synthetic `ProjectGraph` in the test.

3. **Add both tests.**
   - The **contract** test: the rule reports nothing against the real repository
     (`*_RealProjectGraph_ReportsNothing`, `*_ProductionArchitecture_HasNoViolations`).
   - The **acceptance** test: the rule reports exactly the probe or the synthetic violation
     (`*_ProbeArchitecture_Reports*`, `*_GraphWith*_*`). A rule that has never been seen failing is
     not proven.

   Test names follow `MethodName_Scenario_ExpectedResult` (C-3). The requirement id belongs in the
   traceability matrix of the slice, not in the test name.

## Conventions

- Assertions with AwesomeAssertions, no mocking framework for the topology rules — they are pure
  functions.
- All documentation, comments and identifiers in English.
- Warnings are errors. A newly firing analyzer rule is either fixed in the code or switched off in
  the root `.editorconfig` with a justification directly above it (FR-52).
