# Tasks

## 1. A part written and converted as its target takes it (#542)

- [x] 1.1 Write each part of a deconstruction through what its target is and convert it to its target's type, in DeconstructionPattern, and lower an assignment with such a part with temporaries. Verify: `DeconstructionConformanceTests.APart_IsWrittenAndConvertedAsItsTargetTakesIt` runs a dictionary entry target from a record and from a tuple, an entry the indexer replaces, a property target, an int part into a long from a record and from a tuple, and the value of the assignment, on both sides, and its seven defect cases fail against `main`
- [x] 1.2 Declare a converted part of a declaration and of a loop after its destructuring. Verify: the same theory's declaration and `foreach` cases pass on both sides

## 2. Documentation and the suites

- [x] 2.1 One `docs/LEDGER.md` line citing #542, and the wiki's SupportedFeatures page in English and Portuguese on a wiki branch named like this one. Verify: `./scripts/check-openspec.sh` passes and the wiki guards pass with `EQ_WIKI_DIR` on that branch
- [x] 2.2 The suites, each alone and read by its exit code. Verify: Compiler, Web, Server and Conformance exit 0
