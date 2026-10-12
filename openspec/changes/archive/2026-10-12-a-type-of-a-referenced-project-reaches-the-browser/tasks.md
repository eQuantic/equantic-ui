# Tasks

## 1. Measure before changing

- [x] 1.1 Write a two-project fixture (an app and a class library holding a static class with a `static readonly` member, a record, a component and a class that reads a file) and build it with eqc as it is. Verify the emitted page module names `Palette` and `Cursor` with no import, which is #624, and keep the fixture as the tests' input.
- [x] 1.2 Read what MSBuild gives for a project reached directly and one reached through another project. Verify `ReferencePath` carries `MSBuildSourceProjectFile` for both, beside the source tree and from packages.

## 2. One compilation per referenced project

- [x] 2.1 Build a compilation per referenced project from its directory (its `*.cs` outside `bin`, `obj` and nested projects, plus its generated global usings), in the order the assemblies reference one another, and make the app's compilation reference them as compilations in place of their assemblies. Verify with tests that a type of the library read from the app is in source, that the library's `global using` does not reach the app, and that an internal type declared in both is no error.
- [x] 2.2 Answer the semantic model of a tree from the compilation that holds it, and route the places that ask for a declaring tree's model through that. Verify with a test per place that a constant, an inherited default, a struct's writing method and an iterator declared in the library are read from the app's side without an exception.

## 3. Modules on demand

- [x] 3.1 Move the build loop out of `Program.cs` into the compiler library, unchanged in what it writes. Verify the transpiled pins and the dashboard sample's modules are byte for byte what they were.
- [x] 3.2 Scan the referenced projects' declarations into a layer of their own, import a referenced project's name only where a symbol recorded it, and write a referenced project's module when a written module imports it. Verify with the fixture: the four cases of the spec's first requirement emit and import their modules, a type reached through a second project is written, and the class that reads a file is neither compiled nor reported.
- [x] 3.3 Verify the refusals with the fixture: a reached class that reads a file fails at the library's file, a `[ServerOnly]` type fails with EQ2010 at the page, the same name reached in the app and in the library fails with EQ1005 naming both files, and the same name unreached builds. Each test fails against the loop without the on-demand step.
- [x] 3.4 Verify a Server Action's answer of a library record revives a `long`, a `DateTimeOffset` and a `Guid` (the hydration spec names the library's twin), that a library component's `Strings.Brand` lands in the neutral and the `pt-BR` catalogs with the keys read and no others, and that an app and a library that each have a `Strings` with a `Title` keep both texts in the neutral, `pt-PT` and `pt-BR` catalogs, each read showing its own. The last one fails with the id computed per compilation.

## 4. The SDK hands the projects over

- [x] 4.1 Write `equantic.projects.txt` from `ReferencePath` in `Sdk.targets`, without the SDK's own projects and the icon packs, and pass `--projects`. Verify by building a sample: the file lists the library and nothing of the SDK.
- [x] 4.2 Add a sample under `samples/` with a shared class library (the spec's cases on one page, and a second library for the transitive case) and have CI's `samples` job build it. Verify in a browser, from the built app: the page renders on the server, hydrates, a click builds a record of the library and reads its static member, the library's component shows its string, and the console is clean.
- [x] 4.3 Prove the consumer's path: pack a local feed, build a two-project app against it alone, and load it. Verify every eQuantic package restored is the one packed and the page behaves as in 4.2.
- [x] 4.4 Time eqc on the sample and on Cura's solution (five referenced projects), before and after. Verify the time added stays within a second on each.

## 5. The record

- [x] 5.1 Document it in the wiki, in English and Portuguese in one commit on a wiki branch named like this one: a section of BuildFlow on a class library shared by a solution's projects (what crosses, what does not, how to keep server code out), and the SupportedFeatures row. Verify the wiki guards pass against that branch.
- [x] 5.2 File the follow-ups the proposal lists, each under #565 or the feature it belongs to, add the `docs/LEDGER.md` line citing #624, and declare what became public in `PublicAPI.Unshipped.txt`. Verify the docs guards and the public API analyzer pass.
- [x] 5.3 Validate and archive the change before the merge (`./scripts/check-openspec.sh`, `openspec archive`), and verify `openspec/specs/referenced-projects/spec.md` exists with its Purpose.
