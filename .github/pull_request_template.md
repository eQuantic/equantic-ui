<!--
Title: `emoji type: description`, in English — ✨ feat · 🐛 fix · 📝 docs · ♻️ refactor · ✅ test ·
🔧 chore · 👷 ci · ⚡ perf · 💄 style. A squash merge takes it as the commit subject.
-->

## What

<!-- The change, in a few sentences. Say what is different for someone using the SDK. -->

## Why

<!-- The defect, the measurement, or the design question this answers. Link the issue, the audit
     section (docs/ARCHITECTURE-AUDIT.md) or the Flutter row (docs/FLUTTER-PARITY.md) it follows. -->

## Proof

<!-- What you ran, and what it showed. "Tests green" names the projects; a fix names the test that
     failed before and passes now; a change to what a realizer produces names the pin that held it
     byte for byte (goldens, SSR fixtures, transpiled fixtures). -->

## Checklist

- [ ] `dotnet test` on the affected test projects, and `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime` if TypeScript changed (the embedded Bun runs `tsc`, then `vitest run`)
- [ ] This PR's checks include a run named **CI** with jobs in it (`build-packages`, `test (ubuntu-latest)`, `test (windows-latest)`). A workflow whose expression does not parse creates zero jobs and the PR still reads mergeable; GitHub then lists the workflow by its file path instead of `CI`
- [ ] `dotnet build samples/DefaultUIDashboard` (and `PhotonDesktop` / `WalletMobile` if the native track changed) — CI's `samples` job builds all three on macOS only (#149); the other hosts are yours
- [ ] A broken contract has a line in the migration notes (we are in preview: break freely, hide nothing)
- [ ] Wiki pages touched in English AND Portuguese, in the same commit of the [wiki repository](https://github.com/eQuantic/equantic-ui/wiki) (the twin lives at `locale/pt-BR/<Page>-pt-BR.md` there)
- [ ] No "widget" in prose; the project's word is *component*
