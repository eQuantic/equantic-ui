# Design

## Context

eqc (`src/eQuantic.Build/Program.cs`) builds ONE Roslyn compilation: the app's `*.cs` (found by
walking the project directory), its generated global usings and generated sources, the icon packs'
sources where a pack is installed (`--ref-sources`), and the assemblies MSBuild resolved
(`--refs`). It then compiles every file of the app's directory, and every type it finds becomes a
module, which is why an app marks its server code `[ServerOnly]`.

Fifty-one places in `eQuantic.UI.Compiler` decide that a type is the app's own by asking whether it
is declared in source (`Locations.Any(l => l.IsInSource)`, `DeclaringSyntaxReferences`), the one
that says its twin is written among them (`ObjectCreationStrategy.TwinIsWritten`). The emitters
import an app type by its simple name when the dependency scan knows that name became a module
(`ComponentDependencyResolver.IsModule`), and the build refuses two written modules of one name
(`EmittedTwins`, EQ1005).

Measured in Roslyn (two compilations, a record and a static class in the library): read through the
library's assembly, the type is not in source and has no declaring syntax. Read through a reference
to the library's compilation, it is in source, its declaring tree is the library's file, each
compilation keeps its own `global using` directives and internal types, and
`GetTypeByMetadataName` finds it from the app. Asking the app's compilation for the semantic model
of a library tree throws `ArgumentException`.

## Goals / Non-Goals

**Goals:**
- A type of a referenced project is a type declared in source for every rule eqc already has, with
  no rule rewritten.
- A referenced project costs and reports only for what the browser code reaches.
- The build loop runs under test as the CLI runs it.

**Non-Goals:**
- The factory surface (`AppUI`) for a referenced project's components, a `[Page]` or a
  `[ServerAction]` declared there, a source generator's output there, hot reload on its files, and a
  type of an assembly with no source in the build. The proposal lists them, each becomes an issue.
- A library flavour of the SDK. A referenced project is any .NET project today, and gets the
  vocabulary by referencing the SDK's packages itself.

## Decisions

**One compilation per referenced project, referenced as a compilation.** This is how Roslyn itself
models a project reference (`Compilation.ToMetadataReference()`), and what Flutter does with a path
dependency: the package's own source is compiled with the app and only what is reached ships. The
alternative already in the tree is the icon packs' way, adding the sources to the app's own
compilation. It was rejected for whole projects: a library's `global using` would reach every file of
the app, and a name it made ambiguous there becomes an error type, which is the path where eqc
degrades a member call without a word. Two projects that each declare an internal `Guard` would be a
duplicate definition. Per-project compilations have neither problem, which the measurement shows.

**Each project is compiled against the app's reference list.** The list MSBuild resolved for the app
holds what its projects need, since references flow. The project assemblies in it are replaced by
compilation references, in dependency order, and the order is read from the assemblies themselves:
each one MSBuild built names the assemblies it references. Evaluating every project with MSBuild
inside eqc was rejected as slow and as a second source of truth. What it costs: a compile-time-only
dependency of a library that does not flow to the app is unresolved in that library's compilation,
and a reached type that uses it is refused (EQ2006) and not guessed. A project with no C# source,
one written in another language, keeps its assembly.

**A project's friend declarations are read from its assembly.** An `InternalsVisibleTo` item of a
project file is written into a generated file under `obj`, which the C# compiler reads and this
build does not. Left out, an internal type of the library that the app compiles against would be one
this build cannot bind. The declarations are in the assembly MSBuild built, which is already opened
for its references, so they are read there and declared again in the project's compilation.

**A project's source is its directory, read by the rule the app's own source is read by.** Every
`*.cs` under it outside `bin` and `obj`, plus its generated global usings, and without a
sub-directory that holds a project file of its own. Asking MSBuild for the project's `Compile` items
would be more exact for a project that removes or links files, and was left for the day the app's own
source is read that way too: two rules for one question would disagree. The same holds for the
symbols a project's `#if` reads, which no file is parsed with, the app's own included. Both are
filed as one gap (#828).

**The SDK names the projects from what MSBuild already resolved.** Every `ReferencePath` that came
from a project carries the project file that produced it (`MSBuildSourceProjectFile`), direct or
through another project. `Sdk.targets` writes one line per project, the assembly MSBuild resolved
for it and its project file, to `obj/…/equantic.projects.txt`, and passes it as `--projects`. Left
out: a project under the SDK's own source tree, which a sample references in place of packages, and
one whose directory carries the icon pack marker, whose constants are inlined where they are used.
eqc leaves out a project whose directory it already reads another way, whatever the list says. No
property lists projects: the SDK derives them.

**A referenced project's type is written when a written module imports it, and imported only when a
symbol named it.** The build compiles the app's files as today. Each module it writes says which
types of referenced projects it imports: every sibling import of its text that a symbol of the
module's declaration placed in a referenced project, with the file that declares it. The build
compiles that file for that type alone, writes its module, and follows what that one imports in turn,
until none is left. Only a reached type is compiled and claims its name, so a type nobody reaches
neither collides nor reports, and a class beside a reached one in the same file is not compiled
either. The type travels as what it is, its CLR name, and the build compiles the declaration that is
that type: chosen by its simple name, a second type of that name in another namespace of the same
file was compiled with it (found in review).

A module is no longer told apart by its name alone, since the app and a project may each declare a
`Status`. The emitters gather import candidates by name, each path in its own way, and a recording
added to each of them would miss the path nobody thought of. So where a name is declared is read in
one walk of the declaration, apart from all of them: the symbol every name binds to with the type
that declares it (a static member's owner, a constructor's type, the static class an extension method
is declared in) and the type of every expression, each taken apart into its type arguments and
elements. The walk decides nothing. It answers which files declare the type a name is, and the gates
ask it.

The dependency scan of a referenced project is a layer of its own, made when a name of that project
is first asked for: an app name is importable as today, and a referenced project's name only for a
declaration a symbol named. A name a file merely mentions, bound to some package's type, then never
imports a referenced project's module of that name. In a class module a referenced project's type is
imported where the emitted text names it, as the component and the record paths already import: a
constant read off it is written as the literal it is and leaves no reference, and importing it there
compiled a whole class of another project. Measured on a real app.

Two types of one FULL name, one in the app and one in a project, are two types. A reached type of a
project claims its name with its assembly, or the pair would read as one type declared twice, which
is a warning and a module that holds one of the two.

The names are read for the app's own files and the referenced projects'. The libraries the SDK
transpiles beside every app can name no project of the app, and they are most of what a build
compiles.

**A fence that says nothing of "this compilation" asks about the compilation.** The fence that
refuses a `[ServerOnly]` symbol named from client code is silent for a symbol declared in the
compilation being converted, which the scan writes no module for, and it asked that by whether the
type has a source. A referenced project's type has one now, in another compilation, and a page that
called a library's `[ServerOnly]` class built with no diagnostic where it was EQ2010 while the
library was read from its assembly. Measured before it was fixed. The fence asks whether the type is
of the assembly being converted, by the assembly's name, since every file is compiled in a
compilation of its own and two of those hold two assembly symbols for one project. The app's own
`[ServerOnly]` type named from client code builds in silence, as before this change. It is filed.

**The semantic model of a tree comes from the compilation that holds it.** One helper answers it:
the compilation asked, or the one among its compilation references that contains the tree. The
places that ask a compilation for the model of a symbol's declaring tree (a constant's initializer,
an inherited default, a struct's writing methods, an iterator's source) go through it, since that
tree can now be another project's.

**The loop moves into the compiler library.** `Program.cs` holds it in top-level statements today,
which no test can call. It becomes a class of `eQuantic.UI.Compiler.Services` (`AppBuild`) that the
CLI calls and that the new tests run over projects written to a temporary directory, as
`ModuleBundler` was moved for the source map tests. The source guard that watched the CLI's file for
a second walk of the source files goes with the loop to its new file. A host that runs many builds
in one process hands the build a reader that keeps one reference per assembly, since a reference
holds its assembly in native memory until it is finalized.

**A referenced project's catalogue is read for the keys its reached types read.** A read of a
resource is recorded with the path of its designer file, and the catalogs are filled from the `.resx`
beside it, so the strings follow from the component being compiled. The rule that a resource outside
the app's root contributes every key was written for the SDK's own library, whose reads this build
cannot see. A referenced project's reads are seen, so it contributes the keys read.

**A catalogue's id is decided once for the build.** A read of `Strings.Title` is written as the
catalogue's id and the key, and the id is the class's simple name while that is unique among the
resource classes of the compilation the file is compiled in, namespace-qualified once two share a
name. With a compilation per project that view differs by file: an app's `Strings` and a library's
`Strings` would both be `Strings`, and one's `Title` would overwrite the other's in the catalog. So
the ids are computed once, over the resource classes of the app and of every referenced project, and
every file reads that one answer, by the class's assembly and full name, since a symbol does not
compare equal across the compilations a file is recompiled in. An app with one `Strings` and no
referenced catalogue keeps the id it has today.

## Risks / Trade-offs

- [A symbol of a referenced project belongs to another assembly] → The fifty-one judgments ask about
  source, which the measurement answers. One of them meant "of this compilation" and is asked that
  way now, the fence that refuses a `[ServerOnly]` symbol. The tests take each piece of this change
  back in turn and fail without it.
- [A referenced project's file edited after its assembly was built] → eqc reads the file, csc read
  the same file in the same build, and an edit between the two is the next build's.
- [A project with hundreds of server files] → Each is parsed, side by side, and none is bound until
  reached. Timed with one eqc, with and without the list of projects: the sample goes from 4.8 s to
  5.0 s, and a real solution of five referenced projects stays within the noise of a run, 9.1 s
  against 9.8 s at most, where it added two seconds before the names stopped being read for the
  SDK's own libraries. That solution's pages reach none of its projects yet, and what the build
  writes for it is the same files with and without the list.
- [A type that crossed as data is compiled whole] → A record of a referenced project that a Server
  Action answers had no twin and was read back member by member. Its twin is the whole record now, so
  a method of it that a browser cannot run fails the build at that method, though no page calls it:
  measured, with the build handed no projects as the control. Loud, at the right file, and nothing
  written wrong, which is why it stands. Keeping such a type a shape with no twin was weighed and
  left: every rule that asks for a source would have to be told about it, and a value with no twin
  meeting code that calls it fails in the browser. The way through is a member the build leaves out,
  or members compiled only where they are reached, and it is filed (#826).
- ["The emitted text names it" reads inside a string literal] → A word of a string can pass for a
  reference, for the app's own types as for a project's. For a project's it would compile a type
  nothing uses. It is the test every path already had, and it is filed with the import of a
  resource class it produces today.
- [Two ways to build: beside the source tree, and from packages] → The sample proves the first in
  CI. The second is proven with a two-project app against a local feed, as the release gate is
  rehearsed.
- [A library that needs a generated source to compile a reached type] → Unresolved in its
  compilation and refused (EQ2006). It is among the follow-ups.
