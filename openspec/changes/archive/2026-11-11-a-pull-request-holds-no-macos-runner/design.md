# Design

## Where a merge is decided

Flutter's own CI does not run every target on every pull request: a target its `.ci.yaml` marks
`presubmit: false` runs only once a change is on its way in. The same split fits here since #1308,
with the merge queue as the place a change is on its way in: the queue's run includes
the main a pull request lands on, so it is the run whose macOS jobs say anything about main. A pull
request's run repeated them on a head that would move, holding four of the five macOS runners the
plan allows, so every other pull request, and the release's tag, waited for them.

## Skipped under the check's name

A job whose job-level `if` is false reports as skipped, under its name, and a skipped required check
is satisfied. A matrix cannot do that for one leg: a job-level `if` cannot read the matrix, and a
matrix skipped whole reports under its unexpanded name, `build-platform-runtimes`, which no required
check names, so `build-platform-runtimes (macos-latest, Osx64, OsxArm64)` would never report and every
pull request would stay blocked. The macOS leg is therefore a job of its own with that name, and the
legs share their steps through `.github/actions/pack-runtime`, where the workflow_dispatch version and
the ref reach the script through the environment rather than written into it.

## What still needs them

`build-packages` needs both runtime jobs. `publish-github` (a push to main or a run by hand) needs
`build-packages`, `test (macos-latest)` and `samples` to have succeeded, and they run on those events.
`gate-templates` and `publish-nuget` run on a `v*` tag, where every macOS job runs. The `if` lines are
single-line expressions: a `#` inside an `if: |` is a parse error that fails the workflow at startup.

## What a pull request gives up

A red macOS fact now shows in the queue, not on the pull request, and a failed queue run takes the
pull request out of the queue. A pull request that changes the Apple track can ask for every job on
its branch before it enters, with `gh workflow run ci.yml --ref <branch>`.

## Proof

A run on this pull request shows the four checks skipped and the pull request's required checks
satisfied with no macOS runner taken, and a `workflow_dispatch` run on the branch shows the four
running.
