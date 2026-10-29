- **2026-10-29 · The embedded bun is unpacked into the user's cache, under its zip's SHA-256**: every place
  that ran the bun unpacked it beside its zip, and only when no binary was there. From packages that was
  the NuGet package cache, so an app's first build failed against a cache restored read-only, baked into
  an image or shared on a volume ([#364](https://github.com/eQuantic/equantic-ui/issues/364)), and in a
  source tree a worktree went on running bun 1.3.14 after its zip moved to 1.4.3, bundling every module
  with it and failing two cell-width cases nothing else failed
  ([#932](https://github.com/eQuantic/equantic-ui/issues/932)). One owner, `ProgramArchive` behind the task
  `UnpackProgram`, now unpacks it into the user's cache in a folder named by the zip's SHA-256 that
  appears whole in one rename, for the SDK, the Server's bundle, the Runtime's tests and the conformance
  harness alike, and CI builds the web template against a read-only package cache restored from the
  packages it just packed. Proposed and archived through OpenSpec (`openspec/specs/build-programs`).
