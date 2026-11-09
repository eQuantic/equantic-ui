## Context

The bun is the one program the SDK ships zipped: 30 MB compressed, 70 MB unpacked, in each of six
per-OS runtime packages. It has to be on disk and executable before an app's modules are bundled. Four
places unpacked it, each beside its zip and only when no binary was there: `EnsureBunExtracted` in
`Sdk.targets`, `ResolveBunForServer` in the Server's project, `ResolveBunForRuntime` in the Runtime's,
and `JsExecutor.ResolveBun` in the conformance harness.

## How Flutter answers it

The Flutter tool keeps the engine artifacts it downloads under the SDK's `bin/cache`, and beside each
one a stamp of the version it holds. On every run it compares the stamp with the version the tool
expects and fetches again when they differ: what runs is always what the tool was built for, and a
cache that drifted is caught by the comparison, never trusted because a file is there. pub keeps
packages in a cache of the user's own, `~/.pub-cache`, which no project writes into.

The design takes both: a cache of the user's own, and an identity checked on every build. Here the
identity is the zip's SHA-256, written into the folder's name instead of a stamp beside it, so there is
nothing to compare that could disagree with the folder.

## Decisions

- **Keyed by content, not by package version.** A version names the package's bun, but the source tree
  has no version that moves with its zip (#932 is that case). The SHA-256 of the zip is right for both,
  and it makes the same zip in a package cache, in a source tree and in every worktree one folder. The
  digest of 30 MB costs 14 ms a build, measured. `bun-toolchain.json` pins the same digests, so the
  folder of the host's bun is named by the value the pin already checks.
- **The user's cache, never `obj/`.** An app's `obj` is a location the build owns too, but it would hold
  70 MB per project, per configuration, and lose it on every clean. The system's cache directory is where
  .NET's own tools and Playwright keep downloaded programs, and the system may purge it: the next build
  unpacks again. A machine with no home folder has no NuGet cache either; the build fails naming it.
- **Atomic by rename.** The program is unpacked into a sibling folder named `<digest>.partial-<guid>`,
  made executable there (the system's `chmod`, as `LinkDirectory` makes a link, since a zip carries no
  mode MSBuild applies), and renamed to `<digest>`. A rename that finds the folder there means another
  build won with the same bytes, and a partial folder a stopped build left is swept once it is an hour
  old. A folder whose program a hand removed is unpacked again.
- **One owner for the source tree too.** The Server and the Runtime import one targets file that runs the
  same task, ordered by a `ProjectReference` to the tasks project and loaded in a task host that ends with
  the build, as `Sdk.targets` loads it beside a source tree. The harness calls `ProgramArchive` directly.
  A test fails if an `Unzip` comes back into a build file or the harness extracts on its own.
- **No setting.** Nothing a developer writes chooses the cache; the product principle has no place for a
  knob nobody needs to turn.

## Risks / Trade-offs

- Every bun version a machine has built with stays in its cache until the system or the user clears it,
  as NuGet keeps every package version. One per version, shared by every project and worktree, where
  the source tree held one per worktree.
- The proof that a package cache may be read-only needs packed packages, so it runs in CI's
  `build-packages` job, where they are packed, rather than in a test suite.
