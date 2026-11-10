# Tasks

## 1. What a compile reads

- [ ] 1.1 `SourceReads`, the record of one file's compile, and `SymbolSources`, the one path from a symbol to syntax, which notes the file it reads
- [ ] 1.2 Every read of a declaration's syntax, and of a symbol's location by tree or position, goes through `SymbolSources`. Whether a symbol is in source and how many declarations it has note nothing
- [ ] 1.3 `ValueCopies.Mutating` and `ExplicitImplementations.Cache` keep what they read beside the answer and note it into every compile that takes the answer
- [ ] 1.4 The guard: a raw read of a declaration's syntax or of a symbol's locations outside `SymbolSources` fails, and so does a symbol- or compilation-keyed cache that is not listed with its reason. Verify: the guard fails on one raw read put back

## 2. The served build

- [ ] 2.1 The text outside every body of a changed file is compared before and after the save, and any difference compiles every file
- [ ] 2.2 A file that is not changed, read nothing changed and compiled without error keeps its results, warnings, resource uses and discovered resource classes, replayed into the build in a cold build's order
- [ ] 2.3 Tests, each with the dependency forgotten failing: a body of one screen (only it is compiled), a default interface member's body, a struct method's body read by `ValueCopies`, a constant, a constructor the generated factory calls, and a resource string of a file not compiled again. Every module equals a cold build's
- [ ] 2.4 The compiler's suite with its component pins, and the conformance suite, pass

## 3. The proof

- [ ] 3.1 The measurement on the dashboard sample, with the load average beside each number: eqc's part of a save before and after
- [ ] 3.2 The web's ceiling comes down by what was measured, in a pull request of its own once CI has measured it

## 4. Documentation

- [ ] 4.1 One entry for the ledger citing #1281, as a file of its own under `docs/ledger/`
- [ ] 4.2 The wiki's hot reload paragraph in five languages
