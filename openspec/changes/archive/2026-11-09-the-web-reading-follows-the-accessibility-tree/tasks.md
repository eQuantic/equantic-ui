## 1. The reader

- [x] 1.1 `aria-owns`: an owned element read under its owner, after its own children, in the listed order; owned twice stays with the first owner, an owner of an element around it is ignored
- [x] 1.2 A name's last resorts: `title`, then `placeholder` for a field
- [x] 1.3 Live regions as the vocabulary's `Group`
- [x] 1.4 Proof: a test per scenario in `tests/eQuantic.UI.Testing.Tests`, each failing without its rule

## 2. The fixture

- [x] 2.1 `SemanticsParityTests`: the derived set, every `PressableRole`, curated cases and the components plausible arguments cannot read built in their place; both readings aligned by role and name and compared field by field
- [x] 2.2 `semantics-parity.baseline.txt`, by hand, may only shrink, each entry citing the bug it waits on (#1328 to #1333)
- [x] 2.3 `AnnouncementParityTests` removed, its cases carried
- [x] 2.4 Proof: the fixture fails without the ARIA projection, without the invariant culture, with an entry missing, with a stale entry, and when Photon announces a radio as a button

## 3. Documentation

- [x] 3.1 A ledger entry under `docs/ledger/` citing #339

## 4. Close

- [x] 4.1 `openspec validate --all --strict`, the Compiler and Conformance suites through the lock, the solution built with no warning, the samples built
- [x] 4.2 `openspec archive the-web-reading-follows-the-accessibility-tree --yes` before the merge
