# SmartHal.Automation.Sdk

## Target audience

Automation authors. An automation reacts to what the house reports and drives it back, and this
package carries everything needed to write one — nothing more. Its only SmartHal dependency is
`SmartHal.Contracts`.

## Contents

- The capability interfaces
- The trigger and parameter attributes
- The manifest generator
- The host binding

**In slice 0 this package is empty.** It is published so that the version line, the package name and
the dependency direction are fixed from the start; the types listed above arrive from slice 7
onwards.

## Versioning

One version covers the whole repository; it is derived from the git history by GitVersion and is
never maintained by hand. What counts as a breaking change is settled for all three published
packages in [VERSIONING.md](https://github.com/CreativeCodersTeam/smarthal-kernel/blob/main/VERSIONING.md).

## License

Apache-2.0
