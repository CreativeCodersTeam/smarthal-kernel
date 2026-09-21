# Versioning and releases

This document is the reference a review works against. It fixes how SmartHal is versioned, how a
release is cut, and — in the table further down — what counts as a breaking change. All three
published packages are bound by it.

## One version for the repository

The repository carries a single version. The server, the CLI and the three published packages are
built from the same commit and are stamped with the same number (FR-66). There is no per-package
version line, and no artifact is ever released on its own.

## The version comes from the git history

[GitVersion](https://gitversion.net) derives the version from the git history; the configuration
lives in [`GitVersion.yml`](GitVersion.yml) and runs as `GitVersion.MsBuild`, declared once as a
`GlobalPackageReference` in `Directory.Packages.props` so that it applies to every project.

The version is maintained nowhere by hand (FR-67). The repository contains no `<Version>`,
`<VersionPrefix>` or `<AssemblyVersion>` in any `.csproj` or `.props` file, and no version number in
`GitVersion.yml` either; the architecture test
`PropsAndProjectFiles_Parsed_ContainNoHandMaintainedVersion` enforces this. A build needs the full
history, so any clone used for a build — including CI — must fetch with an unlimited depth.

## Branching and releases

The branching model is GitHub Flow (FR-68):

- `main` is always releasable.
- Work happens in short-lived `feature/*` branches that merge back into `main`.
- GitVersion runs in mode `ContinuousDelivery`. Every build that is not on a release tag is a
  prerelease; on a `feature/*` branch the branch name becomes the prerelease label.
- A release is a tag `v<SemVer>` on `main`. The tag is the source of truth — the artifacts, the tag
  and this document can therefore never disagree.

Cutting a release:

```bash
git switch main
git pull --ff-only
git tag -a v0.1.0 -m "Release 0.1.0"
git push origin v0.1.0
```

## Semantic versioning

SemVer 2.0 applies (FR-69). While the version is below `1.0.0`:

| Kind of change | Place that is raised |
|---|---|
| Breaking | Minor (from `1.0.0` onwards: Major) |
| New, backward-compatible functionality | Minor |
| Bug fix without a visible change of surface or format | Patch |

An ordinary commit raises the patch place. A change that the table below classifies as breaking is
announced in its commit message with `+semver: minor` so that GitVersion raises the minor place.

## What counts as a breaking change

The following table is exhaustive and applies to all three published packages. The classification is
made by the review; no tool checks it (FR-70).

| Classification | Change |
|---|---|
| **Breaking** (Major; while `< 1.0.0`: Minor) | A public type or member is removed or renamed |
| | The signature or return type of a public member is changed |
| | A mandatory field is added, or an optional field is made mandatory |
| | The serialization form of a field is changed, or an enum value is changed or removed |
| | A part of the manifest schema is removed or narrowed in what it admits |
| | A member is added to an adapter interface that an existing adapter has to implement |
| **Not breaking** (Minor) | A new public type |
| | A new optional field |
| | A new overload |
| | A new enum value at the end |
| | A member with a default implementation is added to an adapter interface |
| **Patch** | A bug fix without a visible change of surface or format |

## Review checklist

Every change to one of the three published packages is checked against this list before it is merged
(FR-70, NFR-8, NFR-9):

- [ ] The change is classified against the table above, and the classification is stated in the pull
      request. When in doubt, the change counts as breaking.
- [ ] A breaking change carries `+semver: minor` in its commit message and a migration note.
- [ ] No version number was introduced by hand anywhere in the repository.
- [ ] Each package stays as small as its target audience allows and contains only what that audience
      needs (NFR-8). A type that only the server needs does not belong in a published package.
- [ ] Every published package still carries a description, authors, a license expression, a project
      URL, a repository URL and a README that names its target audience (FR-71).
- [ ] The surface added to `SmartHal.Adapter.Sdk` gives an adapter loaded in-process no capability
      that an adapter registered over the bus lacks (NFR-9).

## Packages and symbols

All projects build deterministically, embed source links and set `EmbedUntrackedSources`;
`ContinuousIntegrationBuild` is switched on when the build runs on the hosted runner (FR-72). Each
of the three published packages ships a `snupkg` symbol package alongside its `nupkg`.

### Reproducible packages

The same commit built with the same SDK band produces bit-identical packages (NFR-3) — but only when
`SOURCE_DATE_EPOCH` is set. The compiled output is already reproducible; what is not is the `nupkg`
itself, because NuGet stamps the wall-clock time of the pack run into the archive entries. With the
commit time as the reference point the archive becomes reproducible too:

```bash
export SOURCE_DATE_EPOCH=$(git log -1 --format=%ct)
dotnet pack SmartHal.slnx -c Release -p:ContinuousIntegrationBuild=true -o out
```

Every release build has to export that variable. It is an environment variable that NuGet reads
directly, so it cannot be set from `Directory.Build.props`.

### Acceptance

`eng/verify-packages.sh` is the acceptance instrument for all of this:

```bash
dotnet pack SmartHal.slnx -c Release -o artifacts/packages
eng/verify-packages.sh artifacts/packages
```

It exits 0 only when all five checks report `PASS`.
