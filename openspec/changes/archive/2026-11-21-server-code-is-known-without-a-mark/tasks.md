# Tasks

## 1. Measure on main

- [x] 1.1 A middleware with no mark and a `[GeneratedRegex]` in a `[ServerOnly]` class, in the dashboard sample: EQ2112 and EQ2004, about twenty-five from `RegexGenerator.g.cs`
- [x] 1.2 A component's static called from another namespace through the generator's surface: CS0119 where the namespace comes in by a `using` at the file's top, fine in the component's own namespace

## 2. What the browser reaches

- [x] 2.1 `AppModules` collects the app's compiled types; `AppBuild` writes, claims and reports only what is reached from the components through sibling imports
- [x] 2.2 Tests that relied on every class being a module reach their class from the page; `AModuleOfTheAppNoComponentNames_IsServerCode_AndNoModule` holds the inverse
- [x] 2.3 Verify: writing everything, as main does, fails the inverse; the probes on the dashboard sample build clean, and a page that names the middleware gets its EQ2112; the dashboard's served JavaScript is byte for byte main's

## 3. A component's statics

- [x] 3.1 `ComponentAliases` and the generator's alias for a component with statics, none where a file imports another type of the name
- [x] 3.2 Verify: without the alias the helper is CS0119; with it always, a server file's `Timer` becomes the component (CS1729); the dashboard sample with a probe emits `AppUI.zzEditor(ZzEditor.shout('hi'))`

## 4. Documentation

- [x] 4.1 `CLAUDE.md`'s declarative surface section
- [x] 4.2 The wiki's ServerIntegration and DeclarativeSurface pages in their five languages, on the branch named like this one
- [x] 4.3 One entry for the ledger citing #1779 and #1781, as a file of its own under `docs/ledger/`
