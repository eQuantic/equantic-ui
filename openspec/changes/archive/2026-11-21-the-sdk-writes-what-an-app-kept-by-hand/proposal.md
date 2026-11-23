# Proposal

Closes #1784 and #1788, two sub-issues of the epic #1774 ("Lessons from a real site: what fails only in
the browser is caught by the build and the tests"), and #818.

## Why

An app built on the SDK from an empty repository to production kept three things by hand that the SDK
already knew:

- **Its catalogs' accessors.** eqc turns a `Strings.Hero_Title` read into a lookup in the browser, but
  nothing outside Visual Studio wrote `Strings.Designer.cs`, so the site kept a script that wrote each
  one from its neutral `.resx` and a CI step that failed when the committed file differed. It failed
  once over a stray blank line.
- **Two copies of one version.** The SDK in `global.json` and `EQuanticUIVersion` in
  `Directory.Build.props` had to change together, a rule the site wrote in its own CLAUDE.md, because a
  bump of one alone built and then failed in ways that named neither.
- **`new` for a library's components.** Since #624 a component of a referenced project reaches the
  browser, but only the app's own components had factories, so a page wrote `new Wordmark()` beside
  `Text(…)`.

## What Changes

- **The SDK writes a catalog's accessors** (#1784). A neutral `.resx` with no `Designer.cs` beside it
  gets its accessor class from MSBuild's own `GenerateResource`, in the shape eqc reads (a static
  `ResourceManager` and `Culture`, one static string per key). Its namespace is the root namespace and
  the folder, as Visual Studio names it, and its base name is the manifest name MSBuild gave the
  resource. On the web SDK the class lands in a mirror of the project's folders inside the compiler's
  generated tree, with copies of the `.resx` beside it, where eqc reads a catalog. A `.resx` with a
  Designer beside it keeps it. The native SDK writes the same class. The two templates ship their
  `.resx` without a Designer, and the dashboard and the shared library samples drop theirs.
- **One version, stated once** (#1788). Both SDKs read their own version from their package, as before,
  and now also when the project set `EQuanticUIVersion`: a different one fails with EQ4010, naming both,
  before a restore collects the packages and before a build. The template already states the version
  only in `global.json`.
- **A referenced project's components get factories** (#818). The web SDK names, for the factory
  surface's generator, the assemblies that came from projects (the ones eqc compiles from source,
  without the SDK's own and icon packs). Their public components get factories like the app's own; a
  package's components get none. The app's own component keeps a name both declare (EQ3102 reports the
  other), and the surface stays in the app's namespace. `samples/SharedLibrary` composes `Wordmark()`.

## Capabilities

### New Capabilities

- `resource-accessors`: the class a build writes for a resx catalog, and where eqc finds its catalog.
- `sdk-version`: where an app states the SDK's version, and what a second statement does.

### Modified Capabilities

- `component-factories`: a referenced project's components get factories too.

## Impact

- The SDKs (`Sdk.props` and `Sdk.targets` of both), the factory surface's generator, the templates, the
  samples, `docs/DIAGNOSTICS.md` (EQ4010).
- Public surface: none moves. The developer surface does not move either: `EQuanticUIVersion` is still
  read, now only to compare, and the property the generator reads is internal (`_EqReferencedProjects`).
- A break an upgrading app may meet: a project that sets `EQuanticUIVersion` to a version other than
  the SDK's fails with EQ4010. Migration: remove `EQuanticUIVersion`, and change the version in
  `global.json` alone. An app that keeps its `Designer.cs` changes nothing; one that deletes it gets the
  same class from the build.
- Rules followed: 1 (the developer states the version once, writes no Designer, and composes a
  library's component like their own), 2 (each is a general gap of the SDK, met by one site), 9 (MSBuild's
  own `GenerateResource` writes the accessors, and the version check fails rather than warns), 10 (the
  set of projects is the one the build already resolved, never a list the developer keeps).
