- **2026-10-30 · A screen reader never hears a password on Photon**: a Bug ([#817](https://github.com/eQuantic/equantic-ui/issues/817)).
  A `TextEntry` with `Obscure` drew dots and carried its text in clear in its semantics node, so Android, iOS and
  macOS each handed it to the screen reader as the field's value. The node holds the dots now, one per character as
  the eye counts them, and says `Obscured`, which Android reads as its password flag and macOS as a secure text
  field. Proposed and archived through OpenSpec (`native-accessibility`).
