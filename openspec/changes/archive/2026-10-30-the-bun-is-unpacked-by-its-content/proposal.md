## Why

Closes #364, a bug under #849 (the transpiler's fences hold on every path, part three), and #932, a bug
under #160 (instruments that fail, not warn).

The embedded bun ships zipped in the per-OS runtime package, and every place that ran it unpacked it
beside its zip, only when no binary was there. For an app built from packages that place is the NuGet
package cache (#364): a cache restored read-only by a CI, baked into a container image or shared on a
volume failed the app's first build at the unzip, two builds on a clean cache unpacked into one folder,
and NuGet never knew of a file its package does not hold. For the source tree (#932), the Runtime and
Server projects and the conformance harness each did the same, so a worktree that unpacked bun 1.3.14 in
June went on running it after the tracked zip moved to 1.4.3: it bundled every module with it and failed
two cell-width cases no other worktree failed, with nothing saying which bun ran.

## What Changes

- One owner unpacks the bun, `ProgramArchive` in `eQuantic.UI.Sdk.Tasks`, behind the MSBuild task
  `UnpackProgram`: into the user's cache (`~/Library/Caches/eQuantic` on macOS, `$XDG_CACHE_HOME/eQuantic`
  or `~/.cache/eQuantic` on Linux, `%LOCALAPPDATA%\eQuantic\Cache` on Windows), in a folder named by the
  SHA-256 of the zip, which appears in one rename once the program in it is whole and executable.
- The SDK's `EnsureBunExtracted`, the Server's `BundleRuntime` and the Runtime's `TestRuntime` (through
  one shared `src/Shared/EmbeddedBun.targets`) and the conformance harness all ask it. Nothing is written
  beside a zip any more, and the six runtime packages carry their zip and nothing else from `tools/`.
- For a developer using the SDK: nothing to write or set. An app builds against a package cache nobody
  may write, and the bun it runs is in the user's cache. The first build after an update unpacks the new
  bun once, and every app, package version and worktree carrying the same zip shares it.
- For a framework developer: `dotnet msbuild -restore src/eQuantic.UI.Runtime -t:ResolveEmbeddedBun
  -getProperty:_BunPath` names the bun to run by hand, where `src/eQuantic.UI.Runtime.<Os><Arch>/tools/bun/`
  held one before. A binary an older build left there is ignored.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `build-programs`: gains the requirement "The embedded bun runs from the user's cache, unpacked by its
  content".

## Impact

- **The SDKs**: `eQuantic.UI.Sdk`'s `EnsureBunExtracted` runs `UnpackProgram`, shipped in
  `eQuantic.UI.Sdk.Tasks`. `eQuantic.UI.Sdk.Native` runs no bun and does not change.
- **The packages**: the six `eQuantic.UI.Runtime.<Os><Arch>` packages name their zip; their content does
  not change.
- **The source tree**: the Server and the Runtime projects import `src/Shared/EmbeddedBun.targets`, which
  orders `eQuantic.UI.Sdk.Tasks` before them by a `ProjectReference`; the conformance harness references
  that project.
- **CI**: `build-packages` builds the web template against a read-only package cache it restored from
  the packages just packed (`scripts/check-read-only-package-cache.sh`).
- **eqc, the runtime, the realizers, the Photon shells, the templates**: none.
- **Public surface**: none (`eQuantic.UI.Sdk.Tasks` is a tool, #899). The developer surface does not move.
