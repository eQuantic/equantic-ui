# Tasks

## 1. One decision for hot reload

- [x] 1.1 `UIOptions.HotReloads`: the app's `HotReload`, otherwise Development or `DOTNET_WATCH=1`
- [x] 1.2 The stream, the module cache and the client's configuration (`hotReload`) read it
- [x] 1.3 The boot listens and replays when the configuration says, not when `__EQ_DEV__` does
- [x] 1.4 Check: `HotReloadDecisionTests` and `boot-hot-reload.spec.ts` set the environment against the
      decision; on the old rule the four cases that disagree fail

## 2. eqc's output is build output

- [x] 2.1 The output folder joins `DefaultItemExcludes`, in place of the Content removal
- [x] 2.2 eqc writes in place, and `_EQuanticPruneStaleOutput` removes what it stopped writing, after
- [x] 2.3 Check: `EqcOutputIsBuildOutputTests` runs the shipped targets in MSBuild and evaluates a
      consumer; on main's targets three of its four fail

## 3. The manifest in a workspace

- [x] 3.1 `CompilationSource`, the declarations a compilation holds, asked by every walk of the
      hydration manifest
- [x] 3.2 Check: `AWorkspace_ReadsTheAppTheBuildReads` compiles a page against a referenced project as
      metadata and as a compilation; on main's generators both cases fail with CS8785

## 4. Against the real thing

- [x] 4.1 The dashboard sample under `dotnet watch --no-launch-profile` (Production): an edit to a
      component reloads the page with it, no CS8785, `dotnet watch` stays up; a shared component's edit
      renames its chunk and `dotnet watch` stays up
- [x] 4.2 The wiki's hot reload pages in English and Portuguese, and one `docs/LEDGER.md` line
