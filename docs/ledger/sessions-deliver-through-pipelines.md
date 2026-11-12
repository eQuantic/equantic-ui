- **2026-11-12 · A session can deliver through a registered pipeline of agents, as a proof of concept**: a Task under #160
  ([#1493](https://github.com/eQuantic/equantic-ui/issues/1493)). The repository said what a delivery must satisfy and
  nothing about how a session orchestrates one. A `pipeline` skill now selects a pipeline by the kind of the change,
  runs its stages with seven agents that each declare a model and an effort, holds a gate whose prover is never the
  author, and ships as the Workflow says. Two adversarial agents reviewed the plan before it was written and changed
  it: an area is a duty and not a pipeline, one writer in the tree, the prover alone. A hook refuses a branch created
  outside the convention, and its self-test fails on a path the skill cites that is not there. On the way,
  `scripts/new-issue.py` stopped at a TypeError for an account outside the organization, and reads the types from
  the repository now. Proposed through OpenSpec (`openspec/specs/session-pipelines`), not archived until it is accepted.
