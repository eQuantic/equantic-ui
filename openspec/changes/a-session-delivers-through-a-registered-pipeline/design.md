# Design

## The shape: rules, agents, state

The rules are a skill, loaded when a delivery starts, and not more text in `CLAUDE.md`, which every
session and every subagent already carries whole. The agents are files of this repository, so they
change in the pull request that teaches them something. The state is one ignored folder per
delivery; the truth stays where it already was (the issue, the board, the commits, the OpenSpec
change's tasks, the pull request).

## Decisions, and what the adversarial review changed

Two reviewers read the first plan with a clean context, one against the delivery rules and one
against what Claude Code does. Their findings are on the issue. What they decided:

- **A pipeline is a kind, an area is a duty.** The first plan had `component` and `transpiler`
  pipelines. A transpiler bug on a `fix/` branch would have run `bugfix` and lost the .NET oracle.
  Duties now attach to the paths and to what appears in the diff.
- **One engineer.** Five area engineers differed by one pointer. The area's row gives the rules
  file, the model and the effort, passed on the call.
- **One writer in the tree, no worktree isolation.** A subagent's worktree starts from the default
  branch and does not see the proposal, and the wiki guards find no wiki beside it and pass. Readers
  run in parallel; writers run in turn.
- **The prover runs alone, last.** Its A/B takes the fix out of the tree the reviewers would be
  reading.
- **The guard judges a branch's creation, not a commit.** A commit happens on the wiki's master, in
  a rebase and in other repositories. Main is already refused by the ruleset; the name is the gap.
- **No Stop hook.** Its output at exit 0 does not reach the model, and forcing a continuation would
  hold a session that is waiting for CI.
- **Reports on disk.** Each stage writes its report and answers in a few lines, so the
  orchestrator's context holds decisions and not logs.

## Models and efforts

| Agent | Model, effort | Why |
|---|---|---|
| architect | opus, high | a wrong design is the most expensive rework |
| engineer: compiler, native, SDKs and build | opus, high | the areas where a subtle error costs a CI run or a review round |
| engineer: runtime and web, vocabulary | sonnet, high | the names and the order are already fixed by the design |
| debugger | opus, high | one hypothesis, a reproduction first |
| prover | sonnet, high | it runs commands and reads counts, and a weak A/B is a false green |
| preship-reviewer | sonnet, medium | a checklist; the `code-review` skill covers correctness |
| security-reviewer | opus, high | only where the diff asks for it |
| docs-writer | sonnet, medium | bound by its output, not by reasoning |

## The pilot, and what the owner's review changed

The review's point: merging a skill whose description matches "implement" and "fix" is adopting it
in every session of the repository, and what limits the pace today (the queue, CI's time, five
macOS runners) is what a pipeline can make worse. So:

- **Invoked on purpose only.** `disable-model-invocation: true` until it is adopted.
- **Measured on three real issues**, a bug fix, a feature and a documentation change, against the
  way we work today: wall time and cost per point, defects found after the pull request opens
  (review rounds, queue exits), and compactions. The state file records them as they happen.
- **One build at a time.** Parallel agents read; a build or a suite runs alone, through the host's
  lock when it has one. Parallel debuggers no longer run anything.
- **The macOS run on the branch is counted**, not assumed: the state records whether it caught
  something the queue's own run would not have.
- **Every commit is pushed as it lands**, as a backup: a branch on one disk is lost with the disk.

## Not done

A `release` pipeline (the owner's call). A security audit pipeline.
