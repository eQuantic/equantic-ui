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

- [ ] `dotnet test` on the affected test projects, and `npm run test` in `src/eQuantic.UI.Runtime` if TypeScript changed
- [ ] `dotnet build samples/DefaultUIDashboard` (and `PhotonDesktop` / `WalletMobile` if the native track changed) — CI does not build the samples
- [ ] A broken contract has a line in the migration notes (we are in preview: break freely, hide nothing)
- [ ] Wiki pages touched in English AND Portuguese (`locale/pt-BR/<Page>-pt-BR.md`), same commit
- [ ] No "widget" in prose; the project's word is *component*
