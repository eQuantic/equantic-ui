- **2026-11-12 · A branch is named when it is created, and a contributor can create an issue**: a Task under #160
  ([#1498](https://github.com/eQuantic/equantic-ui/issues/1498)), split from #1493. A branch is `<type>/<slug>` and a
  squash merge keeps no head ref, so a session's hook now refuses the creation of a branch of this repository named
  otherwise, reading the line as the shell does and following `cd` and `-C` to the tree git runs in; a name that
  exists, another repository and what it cannot read pass. Its self-test runs the hook itself in a repository made
  for it, in CI. `scripts/new-issue.py` read the issue types from the organization, which answers null to an account
  outside it, so a contributor's run stopped at a TypeError: it reads them from the repository. Proposed and archived
  through OpenSpec (`openspec/specs/working-agreement`).
