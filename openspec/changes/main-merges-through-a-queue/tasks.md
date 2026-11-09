## 1. The commit range comes from the event

- [ ] 1.1 `check-commit-messages.sh --event`: the range of a pull request, a merge group, a push and anything else, from the event's JSON
- [ ] 1.2 The self-test asserts each event's range, refuses a merge group or a pull request with no base, and finds a bad message in the first of a group's two commits
- [ ] 1.3 `commit-messages` calls `--event`; the A/B: the old expression's range passes that group, the event's range refuses it

## 2. The wiki a merge group reads

- [ ] 2.1 `checkout-wiki.sh` reads a merge group's pull requests from its commits and merges their wiki branches onto master in order
- [ ] 2.2 The self-test, with a stub `gh`: two pull requests' pages merged, a fork's left out, a ref the commits do not end with refused, two conflicting branches refused, nothing left behind on a failure
- [ ] 2.3 Both wiki checkout steps pass the merge group's base, head and ref, and the token

## 3. The workflow and the agreement

- [ ] 3.1 `ci.yml` runs on `merge_group`; every required job runs there and no publishing job does
- [ ] 3.2 The Workflow section of CLAUDE.md and AGENTS.md, and CONTRIBUTING.md, say how a pull request enters the queue
- [ ] 3.3 One ledger entry under docs/ledger/
- [ ] 3.4 WorkflowSectionTests, LedgerTests, DocsIndexTests and both scripts' self-tests pass, and the change is archived
