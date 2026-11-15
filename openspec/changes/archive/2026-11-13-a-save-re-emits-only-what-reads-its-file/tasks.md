# Tasks

## 1. What a compile reads

- [x] 1.1 `SourceReads`, the record of one file's compile, and `SymbolSources`, the one path from a symbol to syntax, which notes the file it reads
- [x] 1.2 Every read of a declaration's syntax, and of a symbol's location by tree or position, goes through `SymbolSources`. Whether a symbol is in source and how many declarations it has note nothing
- [x] 1.3 `ValueCopies.Mutating` and `ExplicitImplementations.Cache` keep what they read beside the answer and note it into every compile that takes the answer
- [x] 1.4 The guard: a raw read of a declaration's syntax or of a symbol's locations outside `SymbolSources` fails, and so does a symbol- or compilation-keyed cache that is not listed with its reason. Verify: `SyntaxReadsTests` fails with one raw read put back and with an unlisted cache added, and found `ResourceClasses.IdCache`, which reads declarations alone

## 2. The served build

- [x] 2.1 The text outside every body of a changed file is compared before and after the save, and any difference compiles every file
- [x] 2.2 A file that is not changed, read nothing changed and compiled without error keeps its results, warnings, resource uses and discovered resource classes, replayed into the build in a cold build's order
- [x] 2.3 Tests, each with the dependency forgotten failing: a body of one screen (only it is compiled), a default interface member's body, a struct method's body read by `ValueCopies`, a constant, a constructor a factory calls, and a resource string of a file not compiled again. Every module equals a cold build's. Verify: `ASaveCompilesWhatItTouchedTests`, and four sabotages each failing its cases (the kept mutation answer's hand-on is not isolated, since the second caller reads the struct's file itself too)
- [x] 2.4 The compiler's suite with its component pins, and the conformance suite, pass. Verify: the compiler's suite, 2858 tests, and the conformance suite, 7432 tests

## 3. The proof

- [x] 3.1 The measurement on the dashboard sample, with the load average beside each number: eqc's part of a save before and after. Verify: a save compiles 1 file of 132, eqc's median from 5.6s (load 56 to 64) to 0.34s, the total's from 5.8s to 0.6s, every warm run under a second, at a load of 126 to 140
- [ ] 3.2 The web's ceiling comes down by what was measured, in a pull request of its own once CI has measured it

## 4. Documentation

- [x] 4.1 One entry for the ledger citing #1281, as a file of its own under `docs/ledger/`
- [x] 4.2 The wiki's ServerIntegration page in five languages
