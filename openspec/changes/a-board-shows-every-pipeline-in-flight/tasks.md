# Tasks

One engineer, the scripts area (opus, high), one writer in the tree. The order is the dependency:
the reader before the server that serves it, the server before the page that reads its shape, the
board before `SKILL.md` cites it (the branch hook's self-test fails on a cited path that is not
there).

## 1. The reader

- [x] 1.1 `.claude/skills/pipeline/board/board.py`: the deliveries of every worktree (`git worktree list --porcelain`), every `.claude/pipelines/*/state.json`, a state that is not JSON listed with its error
- [x] 1.2 The normalising rule of the design: steps, notes, counts, the stage's and the delivery's sums and times, a stage only in `measures`, the delivery's status and Ship's
- [x] 1.3 Claude Code's files under `CLAUDE_CONFIG_DIR` (default `~/.claude`): the agent index by file name, the session registry and its liveness, the attribution by id or `gitBranch`, the agent's stage, the transcript's tail widening from 256 KiB
- [x] 1.4 `--self-test`: the reader's cases over the fixture of the design
- [x] 1.5 Check: `python3 .claude/skills/pipeline/board/board.py --self-test` passes; it fails when a string token is not counted, when a boolean counts, and when `finished` alone decides who is working (each wrong version tried, then restored); against this host's states the totals are #1445 1,214,486, #1522 481,630 (Write 59,501), #677 4,194,093

## 2. The server

- [x] 2.1 `127.0.0.1` only, `--port` (8765), the `Host` check, GET only, the fixed static map, the CSP and `nosniff` on every answer
- [x] 2.2 `/api/pipelines`; `/api/report/<delivery>/<file>` by lookup in the folder's listing (`.md` and `state.json`, regular file, real path inside); `/tokens.css` from `docs/design/tokens.json`
- [x] 2.3 `--self-test`: the server's cases, through the real server on port 0
- [x] 2.4 Check: the real server on this host: `curl` a traversal and a symbolic link out (404), `-H 'Host: evil.example'` (403), and no line of `~/.claude/sessions/*.key` in `/api/pipelines`

## 3. The page

- [x] 3.1 `index.html`, `board.css`, `board.js`: the KPI tiles, Running and All, the list, the graph, the agents working now, the steps of the selected stage, the notes, the reports
- [x] 3.2 `textContent` only; links only from integers and the `origin` remote parsed as a GitHub URL; a poll every 3 s, paused while hidden; no spinner under `prefers-reduced-motion`; every status in text
- [ ] 3.3 Check: the page in Chrome over this host's real states, dark and light, at a phone's width with no horizontal scroll and no console error; a throwaway background agent started in another session appears with its tool and leaves the panel when it finishes

## 4. The skill

- [x] 4.1 Measure `${CLAUDE_SESSION_ID}` by running it: a scratch project with a skill that prints it, `claude -p` invoking it, the printed id compared with the transcript folder written; if it does not expand, drop `session` and keep the link by agent ids
- [x] 4.2 `SKILL.md` section 2: the state records `session` and `measures.merged`, and names the board's command; section 7, step 6: record `merged`
- [x] 4.3 Check: `python3 .claude/hooks/a-branch-is-named-for-its-change.py --self-test` passes (every path the skill cites is there)

## 5. CI

- [x] 5.1 One step in the `roadmap` job of `.github/workflows/ci.yml`, after the hooks' self-tests, with the comment that says what it holds
- [x] 5.2 Check: the step's command exits 1 with the string-token rule broken and 0 restored; `printf '.claude/skills/pipeline/board/board.py\n' | scripts/ci-changes.sh` still says `scope=docs`

## 6. Documentation

- [ ] 6.1 `.claude/skills/pipeline/board/README.md`: how to run it, what it reads, what it never does
- [ ] 6.2 One sentence in `CLAUDE.md`, "Pipelines (a pilot, #1493)", naming the board (outside the Workflow section, so `AGENTS.md` does not move)
- [ ] 6.3 The ledger entry, `docs/ledger/a-board-shows-every-pipeline-in-flight.md`, citing #1524; no wiki page describes the pipeline skill, so no wiki branch
- [ ] 6.4 Check: `dotnet test tests/eQuantic.UI.Web.Tests --filter "FullyQualifiedName~LedgerTests|FullyQualifiedName~DocsIndexTests|FullyQualifiedName~WorkflowSectionTests"`
