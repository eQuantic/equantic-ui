# AGENTS.md

The working agreement for every agent in this repository, and the way into the rest of it.

The project guide is [CLAUDE.md](CLAUDE.md): the product principle that decides designs, the
architecture, the build, the transpiler's rules and the release procedure. Read it before changing
anything, whatever agent you are. This file used to carry its own copy of that guide, and the copy
had fallen months behind (it still documented a Tailwind integration the tree no longer has); a
pointer cannot fall behind.

The section below is the same text as the Workflow section of `CLAUDE.md`, byte for byte.

## Workflow

The working agreement, for every session and every agent. This section is the same text in
`CLAUDE.md` and in `AGENTS.md`, and `WorkflowSectionTests` fails when the two copies differ: change
both in the same commit.

### Language

Talk to Edgar in Brazilian Portuguese in the chat. Everything committed or published is in English:
code comments, XML documentation, Markdown, commit messages, branch names, test names, pull requests
and issues, even where the file around it is in Portuguese. Never a code comment in Portuguese.

### Every change starts from an issue on the board

The board is the [eQuantic UI project](https://github.com/orgs/eQuantic/projects/11). Before the
branch, find the issue the change serves. If there is none, create it as a SUB-ISSUE of the epic or
feature it belongs to, with the right type (Epic, Feature, User Story, Task or Bug), and add it to
the board. Its status moves with the work, in the same step as git: In progress when the branch
starts, In review when the pull request opens, Done when it merges. A sub-issue and a type are set
through GraphQL (`addSubIssue`, `updateIssueIssueType`), and the board status through
`updateProjectV2ItemFieldValue`.

### Branches

`<type>/<slug>`: the type is one of `feat/`, `fix/`, `chore/`, `refactor/`, `docs/`, `test/`,
`ci/`, `perf/` or `build/`, and the slug is the change in words, not an identifier:
`refactor/vocabulary-speaks-no-target`, `fix/the-sdk-does-not-dictate-your-xcode`,
`chore/0.2.0-preview.52`. Never a tool's or an agent's own prefix (`claude/…`, or whatever name a
session suggests): rename it before the first commit. A squash merge keeps no head ref, so the
branch list is the only place this convention is legible.

Nothing goes straight to `main`. A repository ruleset enforces `pull_request` and
`copilot_code_review` there and rejects a direct push; never commit onto `main` locally either.

### Commits

`emoji type: description`, in English, subject and body, emoji first: ✨ feat · 🐛 fix · 📝 docs ·
♻️ refactor · ✅ test · 🔧 chore · 👷 ci · ⚡ perf · 📦 build · 💄 style, and 🔀 merge for a merge
commit.

NEVER a co-authorship or attribution line, in any of these shapes:

```text
Co-Authored-By: <assistant or model name> <noreply@…>
<Assistant>-Session: <link back to a conversation>
🤖 Generated with <tool>
```

…or any other spelling of the same thing. They are placeholders on purpose: this section is pushed
like everything else, so it must not be the one place a real model identifier or conversation link
lives, and a reader who matched only the literal examples would have learned the wrong rule. The
rule is the CATEGORY: no co-authorship for an assistant, no session or conversation link, no tool's
signature line, whatever a harness's own default says. It holds for every artifact (commit messages,
pull request titles and bodies, code comments, documentation); naming the TOOLING in prose stays
allowed. The project's `.claude/settings.json` turns off every attribution Claude Code would add
(the commit trailer, the pull request footer and the session link), so it is not written in the
first place.

And a job compares, because reading it back is not enough: `scripts/check-commit-messages.sh`, run
by the `commit-messages` job, reads every commit message of a pull request and of `main`. Five squash
messages reached main carrying their composer's scaffolding while this rule was already written, and
all five came from the message composed at merge time, the one artefact nobody re-reads. That is why
the merge below composes nothing.

### Identity

Commits are authored and committed as `Edgar Mesquita <edgar@equantic.tech>`, set in the
repository's git config. In a cloud session, `.claude/hooks/session-start.sh` sets that identity in
every new container and turns commit and tag signing off there, because the container's signing key
is not the owner's.

### Pull requests

1. **Review it yourself, then open it yourself.** Before opening the pull request, review the whole
   diff (in Claude Code, `/code-review high` on the branch) and fix what the review finds: Copilot's
   first round starts on its own when the pull request opens, and it should read a diff that has
   already been through a full review. Work that is committed and pushed is parked on a branch
   nobody reviews. Opening the pull request is the last step of the work, so open it without
   waiting to be told, whatever a harness's own default says about not opening one unless asked.
2. **In English, and it closes its issue.** The title is `emoji type: description` and becomes the
   squash commit's subject; the body follows `.github/pull_request_template.md` and says
   `Closes #N`. Then READ BACK the body you posted: a tool may append its own "Generated with…"
   footer when the pull request is created, and that footer is deleted.
3. **Answer Copilot: three rounds at most, and stop at the first without a defect.** Its review is
   light and reveals a pull request a little at a time (69 findings in 47 rounds on six of them,
   #446), so a loop that waits for it to run dry pays one round per finding. Read the WHOLE review,
   its body included, where a finding can live without a thread ("Previously missed"). Sort every
   finding:
   - a **defect**, when the code this pull request changes does the wrong thing, or a guard passes
     where it should fail: fix it, prove the fix both ways, and it earns another round;
   - **hardening, documentation or a nit**, a "Previously missed" item that is not a defect
     included: fix it in the same push, or open an issue under the pull request's parent. It earns
     no round.

   Answer every thread and resolve it (GraphQL `resolveReviewThread`: the ruleset refuses a merge
   while one is open). Put every fix of a round in ONE push, then ask for the next round with
   `gh pr edit <n> --add-reviewer @copilot` and wait for it; it takes a few minutes. The ruleset does
   not review on push, so a merge from main, or a change to the body or the docs alone, costs no
   round. The loop ends at the first round with no defect, and after the third in any case: a
   defect found later is still fixed and proved, without asking for another round, and anything
   else becomes an issue. When the account's GitHub credits are exhausted and Copilot cannot
   review, skip this step.
4. **Squash-merge on green CI, composing nothing.** `gh pr merge <n> --squash` takes the
   repository's squash default, the pull request's title and body, so the commit's subject IS the
   title that was already read. If a message is composed anyway, it goes in a file and
   `./scripts/check-commit-messages.sh --file <file>` reads it before the merge. Then merge the pull
   request's wiki branch, when it has one, into the wiki's master (see Documentation below): main
   and the wiki move in the same step, and the next pull request's guards read both.
5. **A release is Edgar's call.** Merging a pull request never implies one; `CLAUDE.md`'s Version
   Management section says what a bump touches.

Do not open thin pull requests: group a coherent body of work (a slice, a family of bugs, a refactor
and the net that proves it). Commits inside it stay small; the pull request is the unit that must be
substantial.

### CI

`.github/workflows/ci.yml` runs on GitHub Actions. While GitHub Actions has no credits, the same
workflow runs on the eQuantic Space runner, followed with `eqs runs ls`, `eqs runs get <n>` and
`eqs runs logs <n>` (`--job <key>` for one job's full log). The environment has `EQS_API_URL` and
`EQS_TOKEN`, and its setup script installs `eqs`. `eqs runs ls` lists every repository of the
workspace and pull request numbers repeat across them, so confirm the run's `repo` with
`eqs runs get` before reading a failure as this repository's. The Space runner has no Docker daemon.

### Documentation and the ledger

Documentation changes with the behaviour it describes. This repository's Markdown changes in the
same pull request. The wiki page changes in English AND Portuguese, in one commit on a branch of the
wiki repository named exactly like the pull request's own branch. CI checks that branch out for the
docs guards when it exists (`scripts/checkout-wiki.sh`), so the pull request is checked against the
pages it brings, while every other one still reads master, and so does a pull request from a fork,
whose branch name can repeat one of this repository's. When the pull request merges, its wiki
branch is merged into the wiki's master (rebased on master first if master has moved) and deleted.
A page pushed to master before its change merges fails the wiki guards of every other pull request
(#406). Locally the guards read `equantic-ui.wiki` beside the repository, which every local run
shares, so keep that clone on master: to run them against a pull request's wiki branch, point
`EQ_WIKI_DIR` at a worktree of it (`git -C ../equantic-ui.wiki worktree add <dir> <branch>`).
`docs/LEDGER.md` keeps the history, one line per event, citing the issue.

### OpenSpec

Specs are versioned in the repository, under `openspec/`, with the project's context and the rules
for each artifact in `openspec/config.yaml`.

- Every change that creates or changes behaviour starts with a proposal (`/opsx:propose`) in the
  same pull request as the code, and is archived before the merge with
  `openspec archive <change> --yes`, which moves it under `openspec/changes/archive/` and writes its
  delta into the main specs in one step, so `openspec/specs` on `main` always matches the code on
  `main`. `/opsx:archive` walks through the same step by hand; its `mkdir` and `mv` go through the
  session's normal permissions, since a skill's `allowed-tools` pre-approves and forbids nothing.
- A capability gets its spec when a change first touches it, not before.
- The CLI is pinned by `tools/openspec/package-lock.json` and runs through `scripts/openspec.sh`,
  or as `openspec` in a session the hook prepared. CI's `openspec` job validates every change and
  spec with `openspec validate --all --strict`, through `scripts/check-openspec.sh`, which also
  fails when there is nothing to validate.
- Telemetry is off: `OPENSPEC_TELEMETRY=0`, set in `.claude/settings.json`, by the scripts and in CI.

### Sessions start from these rules

A session reads this section from `CLAUDE.md` or `AGENTS.md`, and `.claude/settings.json` registers
a `SessionStart` hook, `.claude/hooks/session-start.sh`, that prepares its environment. In every
session it puts the pinned OpenSpec CLI on PATH. In a cloud container (`CLAUDE_CODE_REMOTE=true`) it
also sets the git identity, turns signing off, installs the .NET SDK that `global.json` pins, and
starts Docker where the container allows it; on a laptop it leaves all of that as the owner set it
up. Every installer it downloads is pinned by version AND SHA-256 and refused on a mismatch, and npm
packages are pinned by the lockfile's integrity hashes. When a tool it is responsible for cannot be
set up, the person in the session sees a warning naming it, and the session still starts. CI's `session-start` job runs the hook the
way a fresh container would and asserts every promise (`scripts/check-session-start.sh`).
