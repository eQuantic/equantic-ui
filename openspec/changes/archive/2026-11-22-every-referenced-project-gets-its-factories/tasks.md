# Tasks

## 1. The list

- [x] 1.1 `_EQuanticFactoryReferences` writes the referenced projects' assembly names one a line in `$(IntermediateOutputPath)equantic.factory-projects.txt`, adds it to `AdditionalFiles` and `FileWrites`, and writes nothing when the app references no project
- [x] 1.2 `Sdk.props` no longer makes `_EqReferencedProjects` compiler-visible, and the property goes
- [x] 1.3 The generator reads every line of the additional file named `equantic.factory-projects.txt` and nothing else
- [x] 1.4 Verify: the SDK's shipped target and props, in a real build of an app referencing a project with no component (`Domain`) before one with a component (`Lib`), give the page its factory; so does a `Lib` whose assembly is named `Brand#Lib`; a design-time build of `Compile` alone hands the compiler the file with both names. Against the code before the fix the three fail, the first with the issue's CS0103; the names joined with `,` instead fail the `#` case, the file written but not handed to the compiler fails all three, the generator reading the first line only fails the first, and the list written at `BeforeBuild` fails the design-time case. Generator tests: a project after the first in the list gets its factories, and a file of another name is no list
- [x] 1.5 Verify the real path: an app beside the source tree with the issue's projects builds, its page's module calls `AppUI.seal(24)`, and the page served in a browser shows "seal 24" and hydrates with no difference; `samples/SharedLibrary` builds and `scripts/check-shared-library-sample.sh` passes

## 2. Documentation

- [x] 2.1 The ledger's entry under `docs/ledger/`
- [x] 2.2 The version mark of the wiki's DeclarativeSurface page, in its five languages, names `_EQuanticFactoryReferences` instead of the property that went, on the branch named like this one
