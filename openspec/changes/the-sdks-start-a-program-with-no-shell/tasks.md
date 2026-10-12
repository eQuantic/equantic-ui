# Tasks

## 1. The task assembly

- [ ] 1.1 `eQuantic.UI.Sdk.Tasks`, netstandard2.0: `RunProgram` (a ToolTask with a program, arguments as items, a folder, `IgnoreExitCode`, `ConsoleToMSBuild`, EQ4004) and `LinkDirectory`
- [ ] 1.2 Write the cases: every character a shell reads reaches a real program as one argument, an empty value is one, the output is collected, a program not on the machine is tolerated under `IgnoreExitCode`, a silent failure is EQ4004, the link is made and what was there stays
- [ ] 1.3 Take the backslash adjustment and the quoting back, and confirm the cases fail
- [ ] 1.4 EQ4004 in docs/DIAGNOSTICS.md

## 2. The web SDK

- [ ] 2.1 The assembly as a graph edge in a source tree and under `tools/netstandard2.0/` in the package, and its `UsingTask`
- [ ] 2.2 `git describe`, bun's `chmod` and `bun add`, the link, eqc in the project's folder with its response file by full path, eqicon's vectors and web icon
- [ ] 2.3 Build the dashboard sample and compare it file by file with a build of main, build the shared-library sample and run its check, and build an app with a `BunPackage` that a page imports

## 3. The native SDK

- [ ] 3.1 The edge, the package folder and the `UsingTask`
- [ ] 3.2 eqicon's calls, `chmod`, `codesign`, `ditto`, `notarytool` (echo off), `stapler`, `hdiutil`, `git describe`
- [ ] 3.3 Build and publish the desktop sample on macOS, the bundle signed, and a disk image

## 4. What the targets hand each program

- [ ] 4.1 Write the cases: the shipped targets, run in a real MSBuild under a folder with every character a shell reads, hand each program exactly its arguments, recorded by stand-in programs on the PATH
- [ ] 4.2 The guard: no `Exec` in the MSBuild files the SDK packages ship

## 5. Proof and documents

- [ ] 5.1 The suites, the solution build, `./scripts/check-openspec.sh`
- [ ] 5.2 The consumer's path from packages alone, with the package folder under a folder named `nuget $b`, served in a browser
- [ ] 5.3 CLAUDE.md, the wiki's Build Flow, Package Architecture and BunPackage pages in English and Portuguese, and one `docs/LEDGER.md` line citing the issue
- [ ] 5.4 Archive the change with `openspec archive the-sdks-start-a-program-with-no-shell --yes`
