## 1. The public API

- [x] 1.1 The build reads every `PublicAPI.Unshipped.*.txt`. Verify: a member moved into a change's file builds, and one in both files is RS0025
- [x] 1.2 The stable-version guard reads every Unshipped file. Verify: `ReleaseContractTests`, whose case of a removal in a change's file fails against main's guard
- [x] 1.3 `public-api.sh update` writes the branch's file and `ship` folds every file. Verify: `scripts/check-release-folds.sh`, nine of whose checks fail against main's script

## 2. Citations

- [x] 2.1 The anchored citations drop their line, in one conversion, and seven ambiguous quotes are lengthened. Verify: `DocsIndexTests`
- [x] 2.2 The guards refuse a number, an ambiguous quote, a stale quote and an undeclared member. Verify: each by mutating the audit
- [x] 2.3 The unanchored counter looks past a citation. Verify: the same 345 on main and on the branch

## 3. The served runtime's budget

- [x] 3.1 The budget is the base and every delta. Verify: `ServedRuntimeBudgetTests`, and growth, shrink and a silent delta each failing
- [x] 3.2 The release folds the deltas. Verify: `scripts/check-release-folds.sh`

## 4. Proof and documentation

- [x] 4.1 The queue simulation over two branches that each add a public member, move a cited line and cross the budget: both land one after the other with no conflict
- [x] 4.2 CLAUDE.md and AGENTS.md, CONTRIBUTING.md, the growth document, the audit's preamble and the wiki's Architecture and BuildFlow pages
- [x] 4.3 One ledger entry citing #1489
