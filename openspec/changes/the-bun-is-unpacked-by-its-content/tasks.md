## 1. One owner

- [x] 1.1 `ProgramArchive` and `UnpackProgram` in `eQuantic.UI.Sdk.Tasks`: the user's cache, a folder per SHA-256, unpacked and made executable beside it and renamed into place. Verify: `ProgramArchiveTests` pass, among them a zip that moves on, a read-only zip folder, eight unpacks at once, a folder that lost its program and a cache that cannot be written

## 2. Every place that runs the bun asks it

- [x] 2.1 `EnsureBunExtracted` in `Sdk.targets` runs `UnpackProgram`. Verify: `TheWebSdk_HandsEachProgramItsArguments_AsTheyAre` finds nothing beside the zip and the bun under the probe's own user cache, and fails on main's targets, where the bun was unpacked beside its zip
- [x] 2.2 The Server's `BundleRuntime` and the Runtime's `TestRuntime` through `src/Shared/EmbeddedBun.targets`, and the conformance harness through `ProgramArchive`. Verify: `NothingElseUnpacksTheBun` passes, and fails on main's project files; `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime` and a build of the Server bundle with the bun from the user's cache
- [x] 2.3 The runtime packages name their zip. Verify: a pack of `eQuantic.UI.Runtime.OsxArm64` holds `tools/bun/bun-darwin.zip` and no binary, beside a binary an older build left in the folder

## 3. Against the real thing

- [x] 3.1 `scripts/check-read-only-package-cache.sh` in CI's `build-packages`. Verify: run locally against packages packed from this tree, it builds the web template with the cache read-only, and it fails against the same packages with main's `Sdk.targets`
- [x] 3.2 `BunToolchainTests` runs the pinned bun as `ProgramArchive` unpacks it. Verify: it reports the pinned version

## 4. Documentation

- [x] 4.1 The wiki's BuildFlow, Architecture, BunPackage and PackageArchitecture pages in all five languages, `CLAUDE.md`, `CONTRIBUTING.md`, the icon generator's usage and the MaterialSymbols readme
- [x] 4.2 One entry for the ledger citing #364 and #932, as a file of its own under `docs/ledger/`
