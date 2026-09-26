<!--
Title: `emoji type: description`, in English — ✨ feat · 🐛 fix · 📝 docs · ♻️ refactor · ✅ test ·
🔧 chore · 👷 ci · ⚡ perf · 📦 build · 💄 style. A squash merge takes it as the commit subject.
The body is in English and carries no attribution: if a tool appended a "Generated with…" footer when
this pull request was created, delete it. The rules are the Workflow section of CLAUDE.md / AGENTS.md.
-->

Closes #

<!-- The issue on the board (https://github.com/orgs/eQuantic/projects/11) that this closes: a
     sub-issue of its epic or feature, with the right type. Create it first if it does not exist. -->

## What

<!-- The change, in a few sentences. Say what is different for someone using the SDK. -->

## Why

<!-- The defect, the measurement, or the design question this answers. Link the audit section
     (docs/ARCHITECTURE-AUDIT.md) or the Flutter row (docs/FLUTTER-PARITY.md) it follows. -->

## OpenSpec

<!-- The change under openspec/changes/<name>/ that proposed this, archived before the merge
     (`openspec archive <name> --yes`), and the capability spec it creates or modifies. A change with
     no behaviour change says so and why (its .openspec.yaml sets `skip_specs: true`). -->

## Proof

<!-- What you ran, and what it showed. "Tests green" names the projects; a fix names the test that
     failed before and passes now; a change to what a realizer produces names the pin that held it
     byte for byte (goldens, SSR fixtures, transpiled fixtures). -->

## Checklist

- [ ] `dotnet test` on the affected test projects, and `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime` if TypeScript changed (the embedded Bun runs `tsc`, then `vitest run`)
- [ ] This PR's checks include a run named **CI** with jobs in it (`build-packages`, `test (ubuntu-latest)`, `test (windows-latest)`, `openspec`, `session-start`, `wiki-checkout`). A workflow whose expression does not parse creates zero jobs, and the ruleset's required checks then keep the PR blocked; GitHub lists the workflow by its file path instead of `CI`. While GitHub Actions has no credits, the same run is on eQuantic Space (`eqs runs ls`)
- [ ] `./scripts/check-openspec.sh` passes: the OpenSpec change validates strictly, and it is archived before the merge
- [ ] `dotnet build samples/DefaultUIDashboard` (and `PhotonDesktop` / `WalletMobile` if the native track changed) — CI's `samples` job builds all three on macOS only (#149); the other hosts are yours
- [ ] A broken contract has a line in the migration notes (we are in preview: break freely, hide nothing)
- [ ] The documentation changed with the behaviour: this repository's Markdown here, and the wiki page in English AND Portuguese, in one commit on a branch of the [wiki repository](https://github.com/eQuantic/equantic-ui/wiki) named exactly like this pull request's branch, merged into the wiki's master when this merges (the twin lives at `locale/pt-BR/<Page>-pt-BR.md` there)
- [ ] One `docs/LEDGER.md` line for this event, citing the issue
- [ ] The diff reviewed by its author before the PR opened (in Claude Code, `/code-review high`); every Copilot thread answered and resolved; a new round asked for (`gh pr edit <n> --add-reviewer @copilot`) only after a defect, and three rounds at most
- [ ] No "widget" in prose; the project's word is *component*
