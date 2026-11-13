# The pipeline board

A local, read-only page that shows every delivery the `pipeline` skill runs: its stages, the agents
working on it now, their tokens and their time (#1524, part of #1493).

## Run it

```bash
python3 .claude/skills/pipeline/board/board.py              # http://127.0.0.1:8765/
python3 .claude/skills/pipeline/board/board.py --port 9000  # when 8765 is taken
python3 .claude/skills/pipeline/board/board.py --self-test  # the board's own check, which CI runs
```

Python 3.9 or later and the standard library; nothing to install. Stop it with Ctrl-C.

## What it reads

- `.claude/pipelines/*/state.json` of every worktree of the repository, and the reports beside it
  (`design.md`, `implement.md`, ...).
- Claude Code's own files under `CLAUDE_CONFIG_DIR` (`~/.claude` by default): `sessions/<id>.json`
  and the transcripts, to tell which agent is working now and on what. A session is running when
  its process is: on Linux, the pid with the start time `/proc` gives; where there is no `/proc`
  (macOS), the pid and a process that `ps` says began within a minute of the session's start, since
  a pid alone is reused.
- `docs/design/tokens.json`, for the page's colours.

The page asks for `/api/pipelines` every 3 seconds, and stops while its tab is hidden.

## What it never does

- It writes no file and answers only `GET`.
- It listens on `127.0.0.1` only, and refuses a request whose `Host` is another name, and an
  `/api/` request a page of another site makes (`Sec-Fetch-Site`).
- No value from a request or from a state is used as a path: each is a key looked up in a listing
  the board made itself, so `..` and a symbolic link out of the folder reach nothing. A worktree
  whose `.claude` or `.claude/pipelines` is itself a link is not read.
- It never sends the session's key files, nor a shell command an agent ran: a `Bash` call shows its
  description, or only its program's name when it has none.
- Every text from a state or a transcript is set as text, never as markup.
