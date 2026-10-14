# Tasks

## 1. Measure

- [x] 1.1 Measure on main, through the build loop eqc runs (`AppBuild`), every case of #816 and #823 in both body forms, on a stateful and a stateless page, and #823's in the app and in a referenced project: what each builds, what it refuses and what the module writes

## 2. The client boundary where the symbol is written

- [x] 2.1 `ServerPlatform` holds the server namespaces, matched by namespace and never by a prefix, with the exceptions left out, and the host-only fence asks it for a member, a construction and an extension's home
- [x] 2.2 The sites that report a member reached through `using static` stop where the fence refuses, and an extension asks the fence before its own EQ2004 verdict, so one site is told once
- [x] 2.3 `SemanticValidator` goes, with its call in `ComponentCompiler`, and the public API says so
- [x] 2.4 `ComponentParser.StaysOnTheServer` is the one rule for what crosses: the emitters of a class, a static class and a record leave a `[ServerOnly]` method, operator and conversion out, the overload check takes no name for one, and the import scans and `NamedSourceTypes` read none of it, nor a component's Server Action body
- [x] 2.5 Tests through the build loop: server code of a page builds on both kinds of page and in both body forms, a `[ServerOnly]` member of a module is left out of its twin, and client code that reaches the server platform is refused once at the site on both kinds of page, in a page's crossing members and in a class a page reaches

## 3. The kept-out rule for a top-level type

- [x] 3.1 `PlainClassModule.NeverCrosses` is the rule, a nested type's owner included, and `ReportIfKeptOut` asks it of every type of source, a top-level component excepted, and the resolver's net reads it by name
- [x] 3.2 The type-level half of the host-only fence answers for metadata types alone, so a type of source is told once, where its name is written
- [x] 3.3 The sites that write a type's name and did not ask: a bare identifier, a name qualified by its namespace, an extension's home, a Server Action stub's parameter, and a component field's zero asked at its declaration
- [x] 3.4 A reference type's zero is null and is free, and a record's net reports a kept-out type its body names
- [x] 3.5 Tests through the build loop: every way of naming a kept-out type is refused once at the page, in the app and in a referenced project, the positions written as nothing build, server code names the types freely, and a component marked `[ServerOnly]` still crosses

## 4. Prove and document

- [x] 4.1 A/B: the new tests fail on the previous commit's source and pass on this one
- [x] 4.2 One fixed case end to end in `samples/DefaultUIDashboard`: before, the build refused it; after, it builds and the module holds the stub alone
- [x] 4.3 `docs/DIAGNOSTICS.md` says what EQ2004, EQ2010 and the boundary codes mean now, and the diagnostics baseline names `ServerPlatform`
- [x] 4.4 The wiki pages that state the boundary, in every language each is written in, in one commit on the branch of this name, and one entry for the ledger under `docs/ledger/`
- [x] 4.5 Run the Compiler, Web and Conformance suites, and build `samples/DefaultUIDashboard`
- [x] 4.6 Archive the change with `openspec archive a-pages-boundary-reads-only-what-crosses --yes`
