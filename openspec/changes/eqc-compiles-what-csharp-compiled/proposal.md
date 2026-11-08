## Why

Closes #828 (under #565) and #1003 (under #849).

eqc found a project's source by walking its directory, and parsed every file with no preprocessor symbol defined. Neither is what the C# compiler was given:

- `#if DEBUG` / `#elif NET10_0_OR_GREATER` / `#else` answered the `#else` branch in the browser in both configurations, while the server answered `debug` in Debug and `net10` in Release (#828).
- A file the project removes with `<Compile Remove>` became a module, and a file it links from outside its directory with `<Compile Include … Link>` was never read (#828).
- The same held for each project the app references since #624, where multi-targeted code under `#if NET8_0_OR_GREATER` and files linked between projects are where it bites first.
- A Release build after a Debug one read `obj/Release/…/GlobalUsings.g.cs` and `obj/Debug/…/GlobalUsings.g.cs` alike, so a configuration's usings reached the other's build (#1003).

## What Changes

- The web SDK writes `obj/…/equantic.sources.txt`: for the app and for each project eqc compiles from source, the project file, its `DefineConstants` (the implicit symbols of its target framework among them, `AddImplicitDefineConstants` runs first) and the full path of each C# `Compile` item, its generated global usings of the configuration being built included. eqc reads it with `--sources`.
- A referenced project's items and symbols are its own, so the SDK asks it: an `MSBuild` call with the global properties the ProjectReference protocol hands it (`SetConfiguration`, `SetPlatform`, `SetTargetFramework`, `GlobalPropertiesToRemove`) and `CustomAfterMicrosoftCommonTargets` pointing at `Sdk/EQuanticProjectSources.targets`, a file imported for that question alone, whose one target returns the project's section and runs nothing else of it. Only the projects eqc compiles are asked.
- eqc (`ProjectSources`) compiles each project from its list, parsed with its symbols, and transpiles the app's list in place of its directory: the compilation, the modules and the dependency scan read one answer. A file is parsed again for transpilation with the options of the tree it replaces (`SemanticModelProvider.OptionsFor`), so a library's file keeps its library's symbols. A list that does not hold, or says nothing of a project the projects list holds, stops the build (EQ0001).
- What a project compiles that is not its own source, a file its build generated under `obj` or C# a package hands it, is listed after `compiled: `: it joins the compilation, so a page may read it, and is never written as a module of the app nor scanned as one, which the walk, that entered neither, always had.
- A listed file that is not on disk is passed over, since a target that writes it may not have run when the compile target runs alone, as hot reload runs it.
- A host with no list (a test, the design host) walks the directory as before.

## Capabilities

### Modified Capabilities

- `referenced-projects`: the build compiles what the C# compiler compiled, for the app and for each referenced project.

## Impact

- Reaches the web SDK (`Sdk.targets`, the new `Sdk/EQuanticProjectSources.targets`, which the package ships under `Sdk/` beside the targets that import it) and `eQuantic.UI.Compiler` (`AppBuildOptions`, `AppBuild`, `ProjectSources`, `ReferencedProject`, `ProjectCompilations`, `ProjectCompilationHelper`, `ComponentDependencyResolver`, `SemanticModelProvider`, `ComponentParser`, `ComponentCompiler`).
- `eQuantic.UI.Compiler` is a tool surface: `GetCompilationUnits` gains a parameter, and `AppBuildOptions.SourcesFile` and `SemanticModelProvider.OptionsFor` are new. No contract assembly and no line of `developer-surface.baseline.txt` moves.
