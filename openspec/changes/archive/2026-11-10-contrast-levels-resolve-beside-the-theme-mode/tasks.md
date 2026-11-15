# Tasks

## 1. The vocabulary

- [x] 1.1 `ContrastLevel`, `ColorToken.Resolve(ThemeMode, ContrastLevel)`, `WithMore`, and PhotonTheme's More palette. Verify: `DesignTokenTests` and `DesignTokenApcaTests` once per level, `HandoffTokenPinTests.More` both ways
- [x] 1.2 The handoff: `color.more` in `tokens.json`, `contrastMore` out of `proposals.json`, the item shipped in `status.json` and on the Foundations page. Verify: `HandoffStatusTests`

## 2. The targets

- [x] 2.1 Photon: the level through `EmitState`, `PhotonHost.Contrast`, read by the macOS, iOS and Android shells. Verify: `TheHostPaintsTheContrastLevelTests`, and the five shells build
- [x] 2.2 The web: the colour variables again under `prefers-contrast: more`, the hover as a variable on both sides. Verify: `ContrastLevelStylesheetTests`, the parity fixtures, and `tests/e2e/tests/contrast.spec.ts` in Chromium
- [x] 2.3 The A/B of each guard in a disposable worktree. Verify: each sabotage fails its guard

## 3. Documentation

- [x] 3.1 The wiki's DesignSystem page in five languages, and the break in Upgrading in two
- [x] 3.2 One entry for the ledger citing #340, as a file of its own under `docs/ledger/`
