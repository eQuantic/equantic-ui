- **2026-10-29 · Every component's configuration reaches its kept instance, and a test hands each one a second
  configuration to prove it**: a task under #160 ([#1142](https://github.com/eQuantic/equantic-ui/issues/1142)).
  A `StatefulComponent` learns what its parent hands it on a rebuild only through its hand-written `AdoptConfig`,
  and nothing checked the list: `CodeEditor` shipped three settings outside it (#1008), and every test passed
  until one handed a second configuration. `AdoptConfigCoverageTests` now does that to every concrete
  `StatefulComponent` beside it, one public `set` or `init` property and one constructor argument at a time, and
  fails naming what the kept instance does not carry; a baseline that only shrinks names what is read once on
  purpose (`CodeEditor.InitialCode`, an `Accordion`'s `openIndex`). Its first run found ten components dropping
  inputs: `Disabled` in `DatePicker`, `DateTimePicker`, `TimePicker`, `Select` and `TextInput`, three more
  `TextInput` settings, `ListView`'s `Overscan`, `Width` and `Height`, `Menu.Placement`, `Accordion.Multiple`,
  `Calendar.Label`, and `CookieConsent`, which had no override at all. Each is carried now: an `init` setting
  is backed by a field the override writes, and `CookieConsent` keeps the fresh instance whole. eqc wrote such
  a field of `SizeValue` as `= undefined`, which `tsc` refuses, where it leaves a property of one alone; it
  leaves the field alone too now. Proved both ways by removing a line from an override, and the emitter's fix
  against the emitter as it was. Proposed and archived through OpenSpec (`element-tree`, `transpiler-classes`).
