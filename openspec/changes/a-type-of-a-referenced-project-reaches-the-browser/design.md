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
and a reached type that uses it is refused (EQ2006) and not guessed.

**A project's source is its directory, read by the rule the app's own source is read by.** Every
`*.cs` under it outside `bin` and `obj`, plus its generated global usings, and without a
sub-directory that holds a project file of its own. Asking MSBuild for the project's `Compile` items
would be more exact for a project that removes or links files, and was left for the day the app's own
source is read that way too: two rules for one question would disagree.

**The SDK names the projects from what MSBuild already resolved.** Every `ReferencePath` that came
from a project carries the project file that produced it (`MSBuildSourceProjectFile`), direct or
through another project. `Sdk.targets` writes one line per project, its assembly's file name and its
directory, to `obj/…/equantic.projects.txt`, and passes it as `--projects`. Left out: a directory
under the SDK's own source tree, which a sample references in place of packages, and one that carries
the icon pack marker, whose constants are inlined where they are used. No property lists projects:
the SDK derives them.

**A referenced project's type is written when a written module imports it, and imported only when a
symbol named it.** The build compiles the app's files as today. Then, for each module it wrote, it
reads the imports of sibling modules, and for a name no module has yet that a referenced project
declares, it compiles the declaring file and writes that type's module, until no import is unmet.
Results are kept per file, and only a reached type is written and claims its name, so a type nobody
reaches neither collides nor reports. The dependency scan reads the referenced projects'
declarations so their names are known, in a layer of their own: an app name is importable as today,
and a referenced project's name only where the semantic sweep or the conversion recorded the type
from its symbol. A name a file merely mentions, bound to some package's type, then never imports a
referenced project's module of that name.

**The semantic model of a tree comes from the compilation that holds it.** One helper answers it:
the compilation asked, or the one among its compilation references that contains the tree. The
places that ask a compilation for the model of a symbol's declaring tree (a constant's initializer,
an inherited default, a struct's writing methods, an iterator's source) go through it, since that
tree can now be another project's.

**The loop moves into the compiler library.** `Program.cs` holds it in top-level statements today,
which no test can call. It becomes a class of `eQuantic.UI.Compiler.Services` that the CLI calls and
that the new tests run over projects written to a temporary directory, as `ModuleBundler` was moved
for the source map tests.

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

- [A symbol of a referenced project belongs to another assembly] → No rule in the compiler compares a
  type's assembly with the compilation's own. The fifty-one judgments ask about source, which the
  measurement answers.
- [A referenced project's file edited after its assembly was built] → eqc reads the file, csc read
  the same file in the same build, and an edit between the two is the next build's.
- [A project with hundreds of server files] → Each is parsed and none is bound until reached. The
  sample and Cura's five projects are timed in the tasks.
- [Two ways to build: beside the source tree, and from packages] → The sample proves the first in
  CI. The second is proven with a two-project app against a local feed, as the release gate is
  rehearsed.
- [A library that needs a generated source to compile a reached type] → Unresolved in its
  compilation and refused (EQ2006). It is among the follow-ups.
