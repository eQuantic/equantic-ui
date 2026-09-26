# The design system lives HERE

This folder is the handoff, not a copy of it. Corrections are made in this repo and flow back to
Claude Design, rather than the other way round — which is what lets the alignment between the design
system and the implementation be a TEST instead of a periodic audit that rots between runs.

Five tests hold it. The first two: `HandoffTokenPinTests` compares every NUMBER in `tokens.json` with the value the
SDK returns, and `HandoffVocabularyTests` checks that every page here speaks the vocabulary's CURRENT
names — the spellings the vocabulary retired are listed in the test itself, with the decision that
retired each. The first correction the second one produced: the A11 Image block still named the
image's accessible text by the property the vocabulary had retired ten days earlier; it is `Label`
now, and the page says so.

The other three close the gaps those two leave. `HandoffSdkCoverageTests` is the pin's other
direction: it reflects over the token classes and fails when the SDK gains a public member that
`tokens.json` neither publishes nor exempts with a reason. `HandoffFigureTests` holds the numbers
PRINTED on the pages: a figure that derives from a token is written as
`<span data-token="avatar.small">24</span>`, and the test compares the text with the token.
`HandoffStatusTests` holds `status.json`, which says what ships and what is still a request: each
entry is checked against the public API both ways, and a page that still calls a shipped item a
request fails, naming the file and the line.

A sixth, `DesignTokenApcaTests`, is the second contrast gate beside the WCAG 2 ratios in `DesignTokenTests`: APCA 0.0.98G, with floors of 75 for body text, 60 for muted text, links and labels, and 15 for a strong border. The dark palette was re-solved to clear it on 2026-09-23.

| File | What it is |
|---|---|
| `tokens.json` | The NORMATIVE token export — colors, space, radius, shape, type roles, elevation, motion, icon, touch, control metrics, variants. Machine-readable on purpose: it is what the implementation is pinned against. |
| `status.json` | What ships and what is still a REQUEST, per block and per open SDK request, each with the probe that proves it. Pinned by `HandoffStatusTests`. |
| `proposals.json` | Values specified ahead of the SDK (spring roles, the More contrast palette). Not pinned; each set moves into `tokens.json` when it ships. |
| `Photon Design System.dc.html` | The design system page. |
| `Photon DS - Foundations / Phase A / Phase B / Phase C.dc.html` | The sections, as the system was split. |
| `Photon Handoff.dc.html` | The handoff page — the per-component blocks the fidelity audit reads. |
| `Handoff Roadmap - Beyond HIG and M3.dc.html` | The proposal of 2026-09-23 for going past Apple's and Google's guidance: Tier 1 (this folder's pins), then the gaps both platforms lead on, then what one write-once tree can prove that neither can. Each proposal is an issue on the board; `status.json` tracks each until its API ships. |
| `samples/` | The designs the sample apps are built against — Wallet (iOS), Studio (macOS), Console (web admin). |
| `frames/` | The device/window frames those pages present inside. |

## `Tokens.handoff.cs`, retired

The design system's C# view of the tokens was not compiled and nothing compared it, so it drifted
the way the audit document used to: `Motion.ExitFor` returned `MotionSpec` there and `int` in the
SDK, the avatar, selection and wheel members were missing, and `Space.Gutter` meant a column gap
there and a screen inset in the SDK. The C# view IS the source,
`src/eQuantic.UI.Primitives/Theme/Tokens.cs`. It remains in git history.

## The earlier single-file export, retired

`Photon-Design-System.dc.html` (378 KB) was the predecessor, not a complement, and it is gone —
verified section by section rather than assumed:

- **Blocks**: it carried A1–A13, B1–B18 and C1–C15. The split set carries those and **C16
  NavigationRail**, which the old export never had.
- **Bulk**: 63,904 characters of plain text against the phases' 73,011 — the split is 14% LARGER,
  before counting Foundations.
- **Phrases**: of 539 substantial phrases in the old file, 483 appear verbatim in the new set. The
  56 that do not are superseded API text (`TypeStyle(Size, Weight, LineHeight, …)` in the old
  parameter order, a bare `Size` enum the handoff itself notes was renamed to `SizeVariant`,
  inline C# blocks now replaced by `tokens.json`), or page chrome — toolbar labels and the
  marketing tail.
- **Concepts**: every idea probed for is carried, and most are carried further — `safe area` 2→12,
  `ColorToken` 15→25, `Spring` 15→27, `MotionSpec` 1→4, `Dynamic Type` 13→18. Nothing present in
  the old is absent from the new.

Keeping it would have left two sources where one is stale, which is the arrangement this move
exists to end. It remains in git history.

## What was deliberately NOT imported

- **`uploads/`** — 31 markdown files fed INTO the design tool as context. 28 are older snapshots of
  our own wiki pages and 3 are repo docs (`CLAUDE.md`, `CompilerEvolution.md`,
  `NATIVE-GPU-ENGINE-PLAN.md`). Importing them would create a second, staler copy of things this
  repo and the wiki already own.
- **The site and marketing pages** — Landing, Docs (and its 1.1 MB of `docs-*.js` data), Playground,
  Post Library, OG Image, LinkedIn Card. Those belong to the site, which has its own repository.
