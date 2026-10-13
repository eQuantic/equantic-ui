# Proposal

Closes #887, a Bug under #849.

## Why

A library the app references could not read anything of a project it references in turn, whenever
its assembly kept no trace of that project. Cura's shared library reads its theme project's font
through a constant and nothing else: on 0.2.0-preview.62 both of Cura's apps stopped with EQ2006,
`'Serif' does not bind`, while the C# compiled. A library that uses a type of a project only inside a
method meets the same, since the reference assembly the SDK hands keeps no method body.

eqc compiles each referenced project from its source, against the projects it reaches, and took
that order from the projects' assemblies. A constant is copied into the code that reads it, so the
reader's assembly names no reference to the project it came from.

## What Changes

- eqc takes which project references which from the graph NuGet's restore wrote into the app's assets
  file, and never from the assemblies. The SDK hands it with the projects
  (`--project-assets $(ProjectAssetsFile)`).
- Projects handed with no graph, or a project the graph does not hold, stop the build with EQ0001.
- The shared-library sample has a third project, `SharedLibrary.Theme`, that the library reads
  through a constant alone, and its CI check reads the value in the page's bundle.

For a developer who uses the SDK: a library reads what it references, and nothing is set by hand.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `referenced-projects`: a library's code reads the projects it references, in the order restore
  wrote.
