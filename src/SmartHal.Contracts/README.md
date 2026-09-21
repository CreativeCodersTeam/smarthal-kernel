# SmartHal.Contracts

## Target audience

The BFF team. Everything a client of the SmartHal server needs in order to speak to it is declared
here, and nothing else: this package carries no runtime behaviour and no dependency on any other
SmartHal package.

## Contents

- API models of the SmartHal server
- The manifest schema
- The bus messages

**In slice 0 this package is empty.** It is published so that the version line, the package name and
the dependency direction are fixed from the start; the types listed above arrive from slice 6
onwards.

## Versioning

One version covers the whole repository; it is derived from the git history by GitVersion and is
never maintained by hand. What counts as a breaking change is settled for all three published
packages in [VERSIONING.md](https://github.com/CreativeCodersTeam/smarthal-kernel/blob/main/VERSIONING.md).

## License

Apache-2.0
