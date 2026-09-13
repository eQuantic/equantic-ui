# The design system lives HERE

This folder is the handoff, not a copy of it. Corrections are made in this repo and flow back to
Claude Design, rather than the other way round — which is what lets the alignment between the design
system and the implementation be a TEST instead of a periodic audit that rots between runs.

| File | What it is |
|---|---|
| `tokens.json` | The NORMATIVE token export — colors, space, radius, shape, type roles, elevation, motion, icon, touch, control metrics, variants. Machine-readable on purpose: it is what the implementation is pinned against. |
| `Tokens.handoff.cs` | The design system's own C# view of the tokens, as exported. Not compiled; kept for comparison. |
| `Photon Design System.dc.html` | The design system page. |
| `Photon DS - Foundations / Phase A / Phase B / Phase C.dc.html` | The sections, as the system was split. |
| `Photon Handoff.dc.html` | The handoff page — the per-component blocks the fidelity audit reads. |
| `samples/` | The designs the sample apps are built against — Wallet (iOS), Studio (macOS), Console (web admin). |
| `frames/` | The device/window frames those pages present inside. |

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
