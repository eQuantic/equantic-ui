# Tasks

## 1. One version

- [x] 1.1 Both SDKs read their own version from their nuspec whether or not the project set `EQuanticUIVersion`, and default it to that version
- [x] 1.2 `EQuanticRequireOneVersion` fails with EQ4010 before `CollectPackageReferences` and `PrepareForBuild` when the two differ; `docs/DIAGNOSTICS.md` and the diagnostics baseline
- [x] 1.3 Verify: for each SDK over a copy of its folder laid out as its package, a project that sets nothing gets the SDK's version, one that sets the same builds, one that sets another in its body or before the SDK fails naming both; a real restore of that project fails with EQ4010 alone; a warning in place of the error, and reading the version only when unset, each fail a case

## 2. The accessors

- [x] 2.1 `EQuanticResourceAccessors` in both SDKs: `StronglyTyped*` metadata on each neutral `.resx` with no Designer beside it, namespaced by the root namespace and folder, based on the manifest name
- [x] 2.2 `_EQuanticMirrorResources` in the web SDK: the `.resx` copied beside the class in the generated tree, before `CoreResGen` and eqc
- [x] 2.3 The templates and the dashboard and shared library samples drop their Designers; the template tests compile the shells with the class the build writes
- [x] 2.4 Verify: the shipped targets in a real build of a plain project write the class, which answers in the neutral culture and in pt-BR, under a folder reached through a symbolic link included; a Designer beside a `.resx` is kept; dropping the mirror, ignoring the Designer, namespacing by the manifest name and the native SDK writing nothing each fail a case; the dashboard's and the shared library's catalogs and modules are byte for byte those built with their Designers, and both pages switch language in a browser

## 3. The factories

- [x] 3.1 `_EQuanticFactoryReferences` writes `_EqReferencedProjects` before the compiler's options; the generator reads it and describes those assemblies' public components
- [x] 3.2 `samples/SharedLibrary` composes `Wordmark()`
- [x] 3.3 Verify: generator tests over a library compiled as metadata (a factory the page composes, no factory for a package, the surface in the app's namespace or its assembly's, the app's own component keeping the name); ignoring the projects, giving every reference factories, narrowing the namespace and ranking the library first each fail a case; the sample's real build writes the factory and its module, and the page shows the library's component in two languages

## 4. Documentation

- [x] 4.1 The ledger's entry under `docs/ledger/`
- [x] 4.2 The wiki's Localization, DeclarativeSurface, PackageArchitecture, GettingStarted and Diagnostics pages in their five languages, on the branch named like this one
