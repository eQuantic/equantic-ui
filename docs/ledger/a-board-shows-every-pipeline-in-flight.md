- **2026-11-13 · A local board shows every pipeline delivery in flight**: a Task under #1504
  ([#1524](https://github.com/eQuantic/equantic-ui/issues/1524)), part of #1493. `board.py`, beside the `pipeline`
  skill, serves on 127.0.0.1 a read-only page of every delivery's state in every worktree of the repository: its
  stages, the agents working now, their tokens and time, and its reports. It reads Claude Code's session and
  transcript files to tell who is working, and no value a request or a state carries is used as a path. The state
  now records `session` and `measures.merged`. Its self-test runs in CI's `roadmap` job. Proposed and archived
  through OpenSpec (`openspec/specs/session-pipelines`).
