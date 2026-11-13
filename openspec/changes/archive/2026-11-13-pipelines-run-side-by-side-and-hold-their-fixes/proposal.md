# Pipelines run side by side, and a gate's fix stays with its finding

Closes #1526, a Task under the epic #160 (Instruments that fail, not warn). It applies what the
pilot measured (#1504) to the `pipeline` skill and its agents, and adds what the pilot showed it
lacked: running several deliveries at once on one machine.

## Why

The pilot delivered a bug fix (#1514), a feature (#1521) and a documentation change (#1523) through
the skill and recorded each stage. Several of its findings are rules the skill did not have:

- **A shared checkout cannot hold two deliveries.** A second session switched the clone's branch
  under a running delivery. The skill also told every agent to work "in the session's own tree", so
  one machine ran one delivery at a time.
- **A fix of one round made the next round's defect, in all three deliveries.** On the feature the
  gate did not converge: a hardening item made a nameless group transparent, that led to a
  decoration rule for every pass-through wrapper, and that led to an id refusal that crashed a page
  where `main` rendered. The scope had to be cut back to round 1's tree.
- **The caps were wrong.** The architect stopped at its 60-turn cap with nothing written, and the
  engineer at its 120-turn cap with no task ticked.
- **Resuming a large context cost more than the fix.** A fresh engineer cost 60 k tokens, against
  about 400 k to resume the author for a fix of a few lines.
- **What Ship read as silence:**
  - a pull request that conflicted with `main` ran no CI;
  - a Copilot request did not register from a contributor's account;
  - a wait loop read an empty answer as "left the queue";
  - four queue exits had to be told apart from defects by hand.
- **Agents left work behind or wrote from memory.** An engineer left a wait loop running, which
  inflated its reported durations to hours. A docs writer cited a wrong parent issue from memory.

## What Changes

- **Each delivery in its own worktree.** Intake creates the branch with its own worktree,
  `../<clone>.wt/<issue>-<slug>`, from `origin/main`, and a wiki worktree beside it when the
  delivery writes a page. Every agent is given that absolute path and works only there. The state
  lives in it. Both worktrees are removed once the pull request merges. The clone's checkout is
  never switched.
- **One build at a time per host.** `scripts/one-build-at-a-time.py` runs a command under a
  `flock` on a file in the user's cache, and every build and suite of every delivery goes through
  it. Deliveries that touch different files run side by side, from one session or several.
- **The gate holds a fix to its finding.** A hardening or nit that would change behaviour, add a
  public member or reach a new file becomes an issue, not a commit. Each later round reviews only
  the previous round's delta. A test the prover finds weak is strengthened by a fresh engineer,
  tests only.
- **Caps and handoffs.**
  - The architect's cap is 100 turns and the engineer's 200.
  - The architect writes its change early.
  - One engineer covers areas that compile together.
  - A small, located fix goes to a fresh engineer when its author's context is past about 200 k
    tokens.
- **Ship.**
  - It confirms CI started and Copilot's request registered.
  - A person's review is sorted like Copilot's.
  - Every queue exit is sorted as infrastructure, flake or defect, with a flake seen twice filed.
- **Agents.** Every agent leaves nothing running. The docs writer cites only issues it read in the
  same run.

## Capabilities

### Modified Capabilities

- `session-pipelines`:
  - "One agent of a delivery builds at a time" becomes one build at a time per host;
  - a delivery runs in a worktree of its own;
  - a fix of the gate is held to its finding.

## Impact

Only the session's tooling: `.claude/skills/pipeline/`, `.claude/agents/`, the new
`scripts/one-build-at-a-time.py` and its self-test in CI's `roadmap` job. Nothing an app builds,
and no assembly, package, setting or template.

## Principles

PRINCIPLES 2 (an instrument that fails): the lock's self-test fails when its holders overlap, and
the branch hook's self-test still fails on a path the skill cites that is not there. PROVING 3: each
rule here was a measurement of the pilot before it was a rule.
