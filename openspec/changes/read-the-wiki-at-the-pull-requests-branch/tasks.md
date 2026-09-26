# Tasks

## 1. The checkout

- [x] 1.1 Write `scripts/checkout-wiki.sh`: the pull request's wiki branch when the wiki has one, master otherwise, and a failure when the branches cannot be listed
- [x] 1.2 Write its self-test against a fixture wiki, each run in a process of its own and proven by the page it read
- [x] 1.3 Check: the self-test fails against four broken variants (always master, a tail match, a fallback when the listing fails, a branch name evaluated), each at its own case
- [x] 1.4 Check: against the real wiki, a pushed branch is read, and an absent branch and a run outside a pull request read master

## 2. CI

- [x] 2.1 Run the script in both jobs that clone the wiki, with the head ref through the environment
- [x] 2.2 Add the `wiki-checkout` job that runs the self-test
- [ ] 2.3 Check: this pull request's own runs say which wiki branch they read

## 3. The flow

- [x] 3.1 Say it in the Workflow section of `CLAUDE.md` and `AGENTS.md`, in `CONTRIBUTING.md` and in the pull request template
- [x] 3.2 Name the wiki branch in the wiki guard's message
- [x] 3.3 Give every guard one locator that reads `EQ_WIKI_DIR`, fails when it names no wiki, and names what it read in a failure
- [x] 3.4 Check: with no variable the guards read the clone beside the repository; with a worktree of #418's wiki branch the Diagnostics guard fails naming that worktree, branch and commit; with a missing directory, and with one that is not a wiki, all six fail
- [x] 3.5 Add the `docs/LEDGER.md` line citing #406
- [ ] 3.6 Archive this change before the merge
