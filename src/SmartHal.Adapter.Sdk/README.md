# SmartHal.Adapter.Sdk

## Target audience

Adapter authors. An adapter translates one device family into SmartHal's vocabulary, and this
package carries everything needed to write one — nothing more. Its only SmartHal dependency is
`SmartHal.Contracts`.

## Contents

- The adapter interface
- The candidate and capability mapping types
- Helpers for both connection kinds

**In slice 0 this package is empty.** It is published so that the version line, the package name and
the dependency direction are fixed from the start; the types listed above arrive from slice 6
onwards.

## Design rule for the surface

The surface of this package is designed so that an adapter loaded in-process has no capability that
an adapter registered over the bus lacks. Both hosting kinds see the same contract; neither is a
privileged path. Every addition to this package is reviewed against that rule, which is proven in
slice 6.

## Versioning

One version covers the whole repository; it is derived from the git history by GitVersion and is
never maintained by hand. What counts as a breaking change is settled for all three published
packages in [VERSIONING.md](https://github.com/CreativeCodersTeam/smarthal-kernel/blob/main/VERSIONING.md).
Note in particular that adding a member an existing adapter must implement is a breaking change,
while adding one with a default implementation is not.

## License

Apache-2.0
