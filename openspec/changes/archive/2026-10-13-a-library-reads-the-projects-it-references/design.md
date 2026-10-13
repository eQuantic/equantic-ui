# Design

## Context

See proposal.md. Since #624 each referenced project is a compilation of its own, built from its
source, and each references the compilations of the projects it reaches, as Roslyn models a project
reference. The question this change answers is where "reaches" comes from.

## Goals / Non-Goals

**Goals:**

- A library binds what it reads of a project it references, whatever it reads.
- The order comes from what .NET already resolved, with nothing set by hand.

**Non-Goals:**

- Reading the project files themselves. MSBuild evaluates a project file, with its conditions,
  imports and Directory.Build files, and a reader of its XML would be a second, partial MSBuild.

## Decisions

**The restore graph, not the assemblies.** The order was read from each project's assembly
references, which name another assembly only where the metadata kept a trace of it. A constant is
copied into the code that reads it, and a reference assembly keeps no method body and no private
member. Measured on Cura's shape: the library's reference assembly and its implementation both
reference `System.Runtime` and `eQuantic.UI.Primitives`, and not the theme project they read.

NuGet's restore walks every project file's references and writes the result into the app's assets
file: its libraries of the `project` type are the projects the app reaches, each named by its project
file's path from the app's, and each target framework lists every library's dependencies. The SDK
already reads the same file for transitive project references, and `$(ProjectAssetsFile)` names it
wherever the build puts it. eqc reads it with System.Text.Json and matches a library to a project of
the list by its project file's full path.

**A missing graph stops the build.** Read as no edge at all, it would compile every library without
the projects it reads, which is this defect again with no diagnostic. A project the graph does not
hold means the graph is older than the build, and stops it too, saying to restore again.

**The test solution hands what the SDK hands.** It wrote each project's implementation assembly and
handed it, where the SDK hands the reference assembly. A test then saw an edge a method body carries,
which the SDK's build never sees. It now writes the reference assembly beside it and hands that, with
an assets file of the shape restore writes.

**A sample that holds the case, read by CI.** `SharedLibrary.Common` reads `SharedLibrary.Theme`
through a constant alone, and the page shows the value. On main's compiler the sample stops with the
same EQ2006 Cura met.

## Risks / Trade-offs

- eqc reads a file NuGet writes. Its shape has held since the third version of the format, and a
  file that does not hold it stops the build with a message naming the file.
