- **2026-11-25 · A hold's, a drag's and a pan's click guards are one owned guard, and leave with their click or their
  owner**: a finger's hold that opened a menu and an armed drag each stopped the click their release ended in with a
  capturing guard that a bare timer removed (600 ms and 50 ms), the shape #1876 found in the table viewport's pan. One
  owned click guard (`dom/click-guard.ts`) now serves all three: it goes, timer and listener, with the click it stops,
  at once when its owner lets go (the context controller, a gesture controller's delegate, a table), and otherwise after
  its time, so no timer of it fires after a spec's page is torn down
  ([#1889](https://github.com/eQuantic/equantic-ui/issues/1889)). Proposed and archived through OpenSpec
  (`openspec/specs/gestures`, `openspec/specs/context-requests`).
