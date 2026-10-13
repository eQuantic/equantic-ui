# Tasks

## 1. The class of every shipping assembly

- [ ] 1.1 `Directory.Build.targets` derives the surface class of a shipping project from its name, lets the project set it, and writes it into the assembly as `AssemblyMetadata("eQuantic.Surface", …)`
- [ ] 1.2 A tool's package description says it is a tool of the SDK, outside the compatibility promise
- [ ] 1.3 A test reads the class from the assemblies the web tests reference, and from the compiler, and fails on an assembly that has none

## 2. The native track experimental

- [ ] 2.1 An experimental assembly carries `[assembly: Experimental("EQX0001", UrlFormat = …)]`
- [ ] 2.2 `eQuantic.UI.Sdk.Native` adds EQX0001 to `NoWarn` and says once in the build that the native track is experimental in 1.0
- [ ] 2.3 The projects of this repository that use a native assembly without the native SDK suppress EQX0001, each saying why
- [ ] 2.4 The samples and both templates build with no warning, and a plain project that uses a native type fails with EQX0001, measured
- [ ] 2.5 The wiki's Photon page says what experimental means in 1.0, in English and Portuguese, under the heading the URL names

## 3. The rule that follows the version

- [ ] 3.1 A build target fails a contract project whose `PublicAPI.Unshipped.txt` holds a `*REMOVED*` line when the version carries no prerelease label, naming the line and the two ways on
- [ ] 3.2 Its cases run it through `dotnet msbuild` on a fixture: a stable version fails, a prerelease builds, a tool builds
- [ ] 3.3 `NoOldShapeSurvivesTests` reads the version from the assemblies it scans: no `[Obsolete]` during a preview, and at a stable version one that carries an `EQO` id, a URL and the major it leaves in. Its rule runs on synthetic input at both versions
- [ ] 3.4 `DeveloperSurfaceContractTests` refuses, at a stable version, a contract line leaving the baseline, and lets a native line go. Its rule runs on synthetic input at both versions
- [ ] 3.5 The scan of obsolete members reads every contract assembly the web tests reference

## 4. The agreement

- [ ] 4.1 `CLAUDE.md`: the section on old shapes says when it inverts and what holds it, and Version Management names the three classes
- [ ] 4.2 One entry for the ledger, as a file under `docs/ledger/`
- [ ] 4.3 Run the Web, Compiler and Native.Engine suites, and build the samples and both templates from the tree
- [ ] 4.4 Archive the change with `openspec archive what-1-0-promises --yes`
