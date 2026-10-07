# Proposal

Closes #627 and #663, two Bugs under #565 (the transpiler's fences hold on every path, continued).

## Why

An app run under `dotnet watch` applied a component's edit to .NET and never to the browser (#627,
reported by the Falei.pt app). Measured on the dashboard sample, three defects stood between the save
and the page:

- Hot reload was on in the Development environment alone, and an app run without a launch profile,
  as `dotnet run --project src/App` and `dotnet watch` run it, is a Production one: eqc never ran
  again, and the browser kept the module compiled before the edit.
- Where it did run, every module, map and chunk eqc wrote again was a file added to the project, and
  `dotnet watch` stopped on the first one (`HotReloadMSBuildWorkspace … Unexpected true`, the open
  dotnet/sdk#55335, measured on a plain `dotnet new web` app as well). And eqc's target emptied the
  output folder before it compiled, so every module answered 404 for the seconds it took, which is
  where `dotnet watch`'s own refresh of the browser landed on every edit.
- In the workspace `dotnet watch` (and an IDE) runs the generators in, a referenced project arrives as
  another compilation rather than as metadata. The hydration manifest took "has syntax" for "is the
  app's own", asked a semantic model of a foreign tree, and failed on every edit of a page whose base
  or callee lived in another project (CS8785, #663): the hot reload went on without its manifest.

## What Changes

- `UIOptions.HotReload` left unset is on in Development and under `dotnet watch`, which sets
  `DOTNET_WATCH` to 1 on the app it runs, whatever the environment. Set, it decides as before.
- That one decision (`UIOptions.HotReloads`) maps the stream that announces a rebuild, sets the cache
  of the modules a rebuild rewrites, and reaches the page as `hotReload` in `window.__EQ_CONFIG`. The
  boot listens, and replays a page's state after a reload, exactly when the server streams rebuilds;
  it asked `__EQ_DEV__` before, which agreed with the server only while the app left `HotReload` unset.
- eqc's output folder is declared as build output, in `DefaultItemExcludes`, in place of the Content
  removal it replaces: the default Content glob, the publish's generated content and `dotnet watch`
  read the same declaration, and `dotnet watch` ignores what eqc writes.
- eqc writes its output in place. A module, a map, a chunk or a culture's catalog it stopped writing
  is removed after it wrote the rest, by its timestamp, as the generated sources already are.
- The hydration manifest reads the declarations the compilation holds (`CompilationSource`), so a
  workspace describes the app a build describes, with the same diagnostics.

For a developer: `dotnet watch` with no launch profile now rebuilds the edited component and reloads
the page, and stays up. Nothing to write. An app that set `HotReload = false` keeps it off.

## Impact

- The Server (`UIOptions`, the client's configuration), the boot (`Resources/boot.ts`), the web SDK's
  targets and the Generators. eqc, the runtime's components, the Photon shells and the templates are
  untouched.
- Public surface: none moves (`HotReloads` and `CompilationSource` are internal). Developer surface:
  `HotReload`'s automatic value widens to `dotnet watch`; no setting is added or removed.
- Not changed, measured and filed: a write-once page's state does not survive a hot reload under any
  runner, since the replay reads a `_state` bag such a page does not have (#664).
