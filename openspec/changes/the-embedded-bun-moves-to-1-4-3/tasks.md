# Tasks

## 1. The six archives and their pin

- [x] 1.1 Take the six archives of the release, check them against its `SHASUMS256.txt`, and repack each as its package holds it
- [x] 1.2 The pin names 1.4.3 and the six digests, and `BunToolchainTests` passes: the digests, and the version the host's binary reports
- [x] 1.3 The regeneration keeps a digest that starts with a digit: write the case, take the fix back and confirm it fails

## 2. The code engine's cell widths

- [x] 2.1 List what the oracle changed between the two versions, character by character
- [x] 2.2 The table follows Unicode 17, and its twin in the runtime is regenerated
- [x] 2.3 `CellWidthOracleTests` passes on both sides, with a Hangul syllable in conjoining jamo as a case

## 3. Proof and documents

- [ ] 3.1 The runtime's own tests, the conformance suite, the Compiler, Web and Server suites, and `./scripts/check-openspec.sh`
- [ ] 3.2 The dashboard sample built and walked in a browser, and the shared-library and desktop samples built
- [ ] 3.3 The wiki's Code Editor page in English and Portuguese, and one `docs/LEDGER.md` line citing the issue
- [ ] 3.4 Archive the change with `openspec archive the-embedded-bun-moves-to-1-4-3 --yes`
