- **2026-11-23 · Text runs right to left, and the web mirrors**: `TextDirection` and `Directionality` join the
  vocabulary, and a component reads its direction from the nearest `Directionality` or, with none, from the UI
  culture, on every target. The server writes the page's `dir` beside its `lang`, the culture data carries the
  direction, and a culture switch in the browser sets it again, with .NET as the one source. The web writes every
  start and end as logical CSS in both producers, so padding, positions, alignment and a shift toward the end mirror
  with that one attribute; tables, sheets, code and diagrams read left to right. The dashboard sample gains Arabic,
  and the Flutter parity row reads SAME for the web; Photon reads the direction and lays out left to right until
  after 1.0 ([#1568](https://github.com/eQuantic/equantic-ui/issues/1568)). Proposed and archived through OpenSpec
  (`openspec/specs/text-direction`).
