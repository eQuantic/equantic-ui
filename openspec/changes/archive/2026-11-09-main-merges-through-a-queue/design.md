## Context

A merge group run is a run on a branch GitHub writes, `gh-readonly-queue/main/pr-<N>-<sha>`, whose
head holds one squash commit per pull request in the group, on top of `main` as the queue last saw it.
The event carries `merge_group.base_sha`, `head_sha` and `head_ref`, and no pull request.

## Decisions

### The commit range comes from the event, in the script

The range used to be chosen by a workflow expression: a pull request's base and head, or else a push's
`before` and `sha`. Under `merge_group` that expression gives an empty base and the group's head, so the
guard would have scanned the last squash commit alone and passed every other one in the group. The
choice moves into `check-commit-messages.sh --event`, which reads `GITHUB_EVENT_NAME`, the event's
JSON and `GITHUB_SHA`, so the self-test holds an event of every kind and asserts the range each one
gives. A pull request or merge group event without a base or a head fails rather than scanning less.

A merge group's range is the squash commits the queue composed, with the messages that will be on
`main`: the merge-time message the guard exists for (#851) is read before it lands, where today it is
only read after, on the push.

### The wiki a merge group reads is master with the group's wiki branches merged

On a pull request the guards read the pull request's own wiki branch (#406). A merge group holds the
code of every pull request ahead of the last one, and their wiki branches are merged into master only
after they merge, so reading the last pull request's branch, or master, compares that code with pages
that do not match it. The run reads master with each pull request's wiki branch merged onto it, in the
group's order: exactly the wiki that will exist once the group merges and each branch is merged in
turn, as the Workflow section says.

The group's pull requests are found from its commits through the API (`compare/<base>...<head>`): a
squash commit's subject ends with ` (#N)`, and a merge commit's starts with `Merge pull request #N`.
The last one found must be the pull request the head ref names, or the run fails instead of guessing.
Each pull request's head is asked of the API, and one from a fork is left out, as on a pull request
run. A conflict between two wiki branches fails the run: two pages that disagree are a defect the
queue should stop.

`gh` reads the API with the run's token, and is on every runner; the self-test stands in for it with a
stub on the PATH, so no test reaches the network.

### No concurrency group, and the publishing jobs stay out

The workflow has no concurrency group, so no queue run cancels another. `publish-github` runs on a
push or by hand, and every release job on a tag, so none of them runs on a merge group.

## Alternatives

- Keep the range in the workflow and add a third arm for `merge_group`: no test could ever reach it,
  which is how an instrument rots green.
- Read only master in a merge group: every pull request that carries pages would fail its queue run.
- Read only the last pull request's branch: the pages of the ones ahead of it would be missing.
