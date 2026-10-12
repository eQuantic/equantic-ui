# Tasks

## 1. eqc reads its command line in the library, and from a file

- [x] 1.1 Write the cases: the arguments read from a command line and from `@<file>`, each value with a dollar sign, a backtick, a bar, a semicolon, quotes, a percent sign and spaces arriving as it is
- [x] 1.2 `AppBuildOptions.ReadArguments` and `AppBuildOptions.FromArguments` hold what `Program.cs` did inline, and the CLI calls them
- [x] 1.3 Declare the two members in `PublicAPI.Unshipped.txt`

## 2. The list of projects is two lines a project

- [x] 2.1 Write the case: a solution under a directory whose name holds a bar, a dollar sign and a space builds with its library compiled from source
- [x] 2.2 The reader of the list takes each project's two paths from two lines, and the fixture writes them so
- [x] 2.3 Take the change back and confirm the case fails

## 3. The SDK hands eqc a file

- [x] 3.1 Split `CompileEQuanticUI` into the inputs, the response file and the run
- [x] 3.2 Write the response file from an ordered item list, and the list of projects in pairs
- [x] 3.3 Write the cases: the shipped target that writes the response file, run in a real MSBuild with those values, is read back by `FromArguments` as it was set, and the shipped command names no value but the compiler and the file
- [x] 3.4 Update the shared-library sample's check for the list's new shape

## 4. Proof and documents

- [x] 4.1 Run the Compiler, Web, Server and Conformance suites, build the dashboard sample and compare it file by file with a build of main, and build the shared-library sample and run its check
- [x] 4.2 Build the shared-library sample from packages alone, under a directory whose name holds a dollar sign, a bar and a space, with an assembly name that holds a dollar sign, and serve it
- [x] 4.3 CLAUDE.md's build pipeline, the wiki's Build Flow in English and Portuguese, and one `docs/LEDGER.md` line citing the issue
- [x] 4.4 Archive the change with `openspec archive the-sdk-hands-eqc-a-file --yes`
