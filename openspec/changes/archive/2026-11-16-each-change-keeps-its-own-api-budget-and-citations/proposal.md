## Why

Two pull requests that each add a public member, each move a line the audit cites, or each take the
served runtime past its budget conflict, though neither touches what the other changed, and the merge
queue ejects one of them (#1489). Each of the three files is one shared place every change rewrites:
the append-only `PublicAPI.Unshipped.txt` of a project, the line numbers of the audit's 468 anchored
citations, and the one number of the runtime's budget. GitHub's merge and the merge queue read no
`merge=union`, the cause `docs/ledger/` met in #892. On 2026-11-12 one entry of the queue would have
ejected five others, and every ejection cost a dequeue, a merge from main, local proofs and a full CI
run.

## What Changes

- A change declares its public API in `PublicAPI.Unshipped.<branch>.txt` beside each project. The
  build reads every `PublicAPI.Unshipped.*.txt` as the analyzer's additional files, and the analyzer
  merges them: RS0016, RS0017, `*REMOVED*` and RS0025 hold across them. `scripts/public-api.sh update`
  writes into the branch's file, declares an entry once across every file, takes an RS0017 line out of
  the file the diagnostic names, removes a file left with nothing but its header, and refuses on
  `main` or a detached HEAD unless `EQ_API_CHANGE` names the change. `ship` folds every file into
  `Shipped` and removes the changes' files.
- The stable-version guard reads every Unshipped file of a contract project, so a removal written only
  in a change's file fails the build at a stable version.
- The audit's and the security review's anchored citations drop their line number: a fenced one is
  `File.cs  code` and is found in the file exactly once, a prose one is `File.cs Member` and the file
  must declare the member, and a `:LINE` beside a quote or a member is refused.
- The served runtime's budget is the base the last release folded plus a delta per change, each in
  `Budgets/served-runtime/<branch>.json` with `deltaBytes` and what they `bought`. The ceiling and the
  floor apply to the sum, and `scripts/fold-runtime-budget.sh` folds the deltas at a release.
- `scripts/check-release-folds.sh`, in CI's publish-packages job, runs `update`, `ship` and the budget
  fold against stubs.

## Capabilities

### New Capabilities

### Modified Capabilities

- `release-contract`: the stable-version guard reads every Unshipped file.
- `working-agreement`: a change writes its own API file and its own budget delta, and a citation
  names the file and the code.

## Impact

- **Public surface**: none. The developer surface does not move.
- **Contributors**: `public-api.sh update` writes a new file per branch; a release runs
  `fold-runtime-budget.sh` beside `fold-ledger.sh` and `public-api.sh ship`.
- **Docs**: CLAUDE.md and AGENTS.md (Workflow and Version Management), CONTRIBUTING.md,
  `docs/SERVED-RUNTIME-GROWTH.md`, the audit's preamble, and the wiki's Architecture and BuildFlow
  pages in five languages.
