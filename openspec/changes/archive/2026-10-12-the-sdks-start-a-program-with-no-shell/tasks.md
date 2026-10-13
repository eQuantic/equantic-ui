# Tasks

## 1. The task assembly

- [x] 1.1 `eQuantic.UI.Sdk.Tasks`, netstandard2.0: `RunProgram` (a ToolTask with a program, arguments as items, a folder, `IgnoreExitCode`, `ConsoleToMSBuild`, EQ4004) and `LinkDirectory`
- [x] 1.2 Write the cases: every character a shell reads reaches a real program as one argument, an empty value is one, the output is collected, a program not on the machine is tolerated under `IgnoreExitCode`, a silent failure is EQ4004, the link is made and what was there stays
- [x] 1.3 Take the backslash adjustment and the quoting back, and confirm the cases fail
- [x] 1.4 EQ4004 in docs/DIAGNOSTICS.md

## 2. The web SDK

- [x] 2.1 The assembly as a graph edge in a source tree and under `tools/netstandard2.0/` in the package, and its `UsingTask`
- [x] 2.2 `git describe`, bun's `chmod` and `bun add`, the link, eqc in the project's folder with its response file by full path, eqicon's vectors and web icon
- [x] 2.3 Write the case: an app that declares two npm packages has both handed to bun, with a bun that makes `node_modules` as bun does
- [x] 2.4 Build the dashboard sample and compare it file by file with a build of main, and build the shared-library sample and run its check

## 3. The native SDK

- [x] 3.1 The edge, the package folder and the `UsingTask`
- [x] 3.2 eqicon's calls, `chmod`, `codesign`, `ditto`, `notarytool` (echo off), `stapler`, `hdiutil`, `git describe`
- [x] 3.3 Build and publish the desktop sample on macOS, the bundle signed, and a disk image

## 4. What the targets hand each program

- [x] 4.1 Write the cases: the shipped targets, run in a real MSBuild under a folder with every character a shell reads, hand each program exactly its arguments, recorded by stand-in programs on the PATH
- [x] 4.2 The guard: no `Exec` in the MSBuild files the SDK packages ship

## 5. Proof and documents

- [x] 5.1 The suites, the solution build, `./scripts/check-openspec.sh`
- [x] 5.2 The consumer's path from packages alone, with the package folder under a folder named `nuget $b`, an app icon, a title with a dollar sign and an npm package, served in a browser
- [x] 5.3 CLAUDE.md, the wiki's Build Flow, Package Architecture and BunPackage pages in English and Portuguese, and one `docs/LEDGER.md` line citing the issue
- [x] 5.4 Archive the change with `openspec archive the-sdks-start-a-program-with-no-shell --yes`
