# Design

## Worktrees, not the Agent tool's isolation

The Agent tool's `isolation: worktree` branches from the default branch, so an agent there would not
see the delivery's proposal. The orchestrator makes the delivery's worktree itself at intake, on the
delivery's branch, and hands every agent its absolute path. A subagent's shell starts in the
session's directory, so the agents are told to `cd <tree> &&` every command and to address files
absolutely. The folder `../<clone>.wt/` keeps every delivery's tree beside the clone and out of it.

The state stays where it was, `.claude/pipelines/<issue>-<slug>/`, now inside the delivery's
worktree, which `.gitignore` already covers. Whatever reads deliveries in flight reads that folder
of every worktree (`git worktree list`).

Every worktree sits under `../equantic-ui.wt/`, which `.claude/settings.json` adds to the
directories of a session started in the clone (`permissions.additionalDirectories`, applied after the folder is trusted): reads need
no prompt there, and edits follow the session's permission mode as they do in the clone. The wiki
guards look for `equantic-ui.wiki` beside the tree they run in and pass in silence when none is
there, so intake makes the delivery's wiki worktree too, from a freshly fetched `origin/master`,
and every command runs with `EQ_WIKI_DIR` set to it. The state lives in the worktree and goes with
it, so the measures are posted on GitHub before the worktree is removed; a board of deliveries in
flight (#1525) then never shows a finished one. The lock's file is inherited by the command it
guards, so a killed wrapper does not free the host while its build still runs.

The branch hook judges `git worktree add -b`, and it compares repositories by their common git
directory, so a worktree of this clone is held to the convention like the clone.

## One lock per host

A per-delivery rule ("one agent of a delivery builds at a time") let two deliveries build at once.
The lock is `flock` on `$XDG_CACHE_HOME/equantic-ui/one-build-at-a-time.lock` (or `EQ_BUILD_LOCK`),
taken by a small Python wrapper, so it works on Linux and macOS and is released when its holder dies.
Without `fcntl` (Windows) it runs the command unlocked and says so: it serialises builds, it never
stops one. A person's own suite lock replaces it when named once.

## A fix is held to its finding

The pilot's feature took five rounds because hardening items changed behaviour inside a round, and
every behaviour added there was new code nobody had designed or reviewed. The rule is about scope,
not severity: a defect is still fixed and proved; what may not ride along is a change no finding
asked for. A later round reviews only its predecessor's delta and says first whether that delta did
more than its findings asked. The two-round stop stays. It gives way only for a defect whose fix the
finding states exactly and that touches only the lines of the previous round's own fix: in the docs
delivery, a clause, where a stop would have cost more than the fix.

## Not done

- A board of deliveries in flight: #1525, which reads every worktree's state.
- Splitting one delivery's engineers across worktrees. One writer per tree stays the rule.
- Raising a cap per call. The Agent tool takes no turn limit, so the caps are the agents'
  frontmatter.
