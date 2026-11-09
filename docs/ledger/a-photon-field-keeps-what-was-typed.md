- **2026-11-09 · A Photon field keeps what was typed until the page builds it**: a Photon field drew
  the value the app handed it, so a key showed only once something built the component that held the
  value, and the host, unable to see which one, rebuilt every component after a field's handler that
  called no `SetState`. A field's position now keeps what was typed, and the field shows it until the
  page builds it again, when it shows what the page holds whether or not that value changed: the rule
  the web settled in #1026, not the issue's "take the app's value only when it differs", which against
  the tests left `ab!` after a rejected key and `123` over a field reset in the same frame as the
  typing. The measure, the paint, the semantics, the caret and the phone keyboard's editing value read
  the one text, and the position decides whether the page built the field, so a reader holding an
  older frame's node cannot wipe what was typed. Review found that `TextInput`, rebuilding on its own
  when the focus left, built the field from the value its parent had handed it before the typing, and
  a form keeping the value without `SetState` lost it; `TextInput` now keeps what its field reported
  until its parent configures it again, on both targets. A frame after a key typed into a field whose
  form keeps the value without `SetState` went from 96,992 to 30,864 bytes and builds nothing
  ([#996](https://github.com/eQuantic/equantic-ui/issues/996)). Proposed and archived through
  OpenSpec (`openspec/specs/text-entry`, `openspec/specs/element-tree`).
