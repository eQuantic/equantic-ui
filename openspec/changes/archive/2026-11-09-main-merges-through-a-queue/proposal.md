## Why

Closes #1308, a Task under #160 (Instruments that fail, not warn).

The ruleset on `main` requires thirteen of CI's jobs on a head that is up to date with `main` (#287).
With two fronts merging at once (the Release 1.0 subsections and the bug sweep), every merge leaves
every other green pull request behind: each merges `main` again and waits about thirty minutes of CI
before its turn, so the fronts take turns and `main` takes about two merges an hour. GitHub's merge
queue tests a pull request together with the ones ahead of it and merges it when that run is green,
several groups at once, and nobody merges `main` into a pull request only to be up to date.

The queue runs the required checks on the `merge_group` event. `ci.yml` does not listen to it, so
turning the queue on as things stand would merge nothing. Two jobs also read the pull request the
event names, and a merge group names none: `commit-messages` would scan only the group's last
commit, and the docs guards would read the wiki's master without the pages of any pull request in
the group.

## What Changes

- `ci.yml` runs on `merge_group` (`checks_requested`). Every required job runs and reports there, and
  the publishing jobs, which run on a push to `main` or a tag, stay out of it.
- `scripts/check-commit-messages.sh --event` reads the range from the event that started the run: a
  pull request's base and head, a merge group's base and head (the squash commits the queue composed,
  read before they reach `main`), a push's before and after. `commit-messages` calls it instead of
  choosing the range in the workflow, where no test could reach the choice.
- `scripts/checkout-wiki.sh`, in a merge group run, finds the pull requests the group puts on `main`
  from its commits, keeps those from this repository, and reads the wiki's master with each one's wiki
  branch merged onto it in the group's order: the wiki as it will be once those pull requests merge.
  A group whose commits do not end with the pull request its ref names, or whose wiki branches
  conflict, fails and leaves nothing behind.
- The Workflow section (CLAUDE.md and AGENTS.md) and CONTRIBUTING.md say how a reviewed, green pull
  request enters the queue once ruleset 21198869 turns it on, and that nobody merges `main` into it
  only to be up to date.

Nothing changes for a developer using the SDK: no package, no public signature and no line of the
developer surface moves. The change is the repository's own CI and working agreement.

## Impact

`.github/workflows/ci.yml`, `scripts/check-commit-messages.sh`, `scripts/checkout-wiki.sh`, the
Workflow section of `CLAUDE.md` and `AGENTS.md`, `CONTRIBUTING.md`, the `working-agreement` spec and
one ledger entry. The ruleset itself is Edgar's to change, after this merges.
