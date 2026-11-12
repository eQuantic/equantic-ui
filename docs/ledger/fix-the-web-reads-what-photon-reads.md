- **2026-11-11 · The web reads a tooltip, a group and a field's message as Photon does**: three
  differences the semantics parity fixture named. A closed tooltip, hidden with opacity alone, stayed
  in the web's reading order ([#1329](https://github.com/eQuantic/equantic-ui/issues/1329)): its panel
  now carries `visibility:hidden` at rest. A table view read as a group gave its cells band by band on
  the web ([#1331](https://github.com/eQuantic/equantic-ui/issues/1331)): one owner now orders them row
  by row, as Photon reads them. A field's message was written twice on the web and never announced on
  Photon ([#1332](https://github.com/eQuantic/equantic-ui/issues/1332)): `TextInput` draws it in a
  `LiveRegion`, and the web's description twin is hidden. The parity baseline is 23 entries shorter. Proposed
  and archived through OpenSpec (`openspec/specs/screen-reader-parity`, `openspec/specs/table-viewport`).
