- **2026-11-24 · The vertical arrows step a radio group and a tab strip forward on Photon, as on the web**: Photon's
  host mapped ArrowUp to +1 for every `Adjustable`, so a vertical `RadioGroup` or `Tabs` strip stepped backwards,
  where the web steps a tab list's and a radio group's vertical arrows in reading order. Both targets read one table
  now, `AdjustableKeys`, beside `NavigableKeys`: left and right step back and forward, a slider's up raises its
  value, a tab list's and a radio group's down is the next item. Right to left keeps the web's rule, which is
  WAI-ARIA's ([#1873](https://github.com/eQuantic/equantic-ui/issues/1873)). Proposed and archived through OpenSpec
  (`openspec/specs/adjustable-keyboard`).
