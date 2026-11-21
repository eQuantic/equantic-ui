# Proposal

Closes #1801, a Bug under #1587 ("The transpiler's fences hold on every path, part five"), left by #1784.

## Why

Since the SDK writes a catalog's accessors (#1784), an app whose neutral `.resx` has no `Designer.cs`
beside it loses hot reload. Every save under `dotnet watch` ends with "Unable to apply changes due to
compilation errors" and CS0234 on the catalog's namespace, so no edit reaches the server, and the page
the rebuild reloads draws over the server's markup instead of hydrating it. `samples`' web "Edit to
view" measured a warm median of about 5,050 ms against its 1,500 ms ceiling, on CI and locally, and the
templates ship without a Designer, so a new app meets it on its first save.

MSBuild's `CoreResGen` writes the accessor class and adds it to `Compile`, inside `PrepareResources`,
which a build runs before `Compile` but `CompileDependsOn` does not name. `dotnet watch` loads the
compilation it applies an edit to from a design-time build of `Compile` alone, as an editor does, so
that compilation held no accessor class. A `Designer.cs` kept beside the `.resx` was a source file,
which every loader sees.

## What Changes

- Both SDKs run `PrepareResources` before `CoreCompile` (`EQuanticResourceAccessorsCompiled`), so a
  build of `Compile` alone writes the accessors and compiles them. A full build has already run it, so
  nothing changes there.
- A developer writes nothing: hot reload applies an edit again in an app whose catalog has no Designer,
  and an editor that loads the project sees the class.

The SDKs' targets are the only part reached. The public surface and the developer surface do not move.

## Principles

The structural fix rather than the patch (docs/PRINCIPLES.md): the class is in the compilation every
loader builds, rather than a source file written back into the project or a warning to keep a Designer.
