# Tasks

## 1. The instrument

- [x] 1.1 `AdoptConfigCoverageTests` hands every concrete `StatefulComponent` beside it a second configuration,
      one public `set` or `init` property and one constructor argument at a time, and fails naming what the
      kept instance does not carry; a component that keeps the fresh instance whole counts as carrying all
- [x] 1.2 `FactoryArguments` varies a list and an abstract slot that holds a value
- [x] 1.3 The baseline names `CodeEditor.InitialCode` and `Accordion(openIndex)` with their reasons; the
      regenerator only removes
- [x] 1.4 A fixture component that forgets a property, wires another to the wrong source and drops a
      constructor argument is named for exactly those three, and one that keeps its configuration whole for
      nothing

## 2. The components

- [x] 2.1 `Disabled` carried by `DatePicker`, `DateTimePicker`, `TimePicker`, `Select` and `TextInput`
- [x] 2.2 `TextInput`'s `Autofocus`, `Obscure` and `OnFocusChanged`; `ListView`'s `Overscan`, `Width` and
      `Height`; `Menu.Placement`; `Accordion.Multiple`; `Calendar.Label`
- [x] 2.3 `CookieConsent` keeps the fresh instance whole and reads its settings from it
- [x] 2.4 eqc writes a component's field of a struct with no zero twin, with no initializer, as the slot
      left alone (`_width!: SizeValue;`), as it writes a property of one; `StructDefaultTests` holds it, and
      fails against the emitter as it was
- [x] 2.5 Check: with one line removed from a component's `AdoptConfig`, the test fails naming it; with the
      line back it passes. The transpiled components are regenerated, `tsc` and the runtime's own tests run
      on them, and the served runtime stays inside its budget

## 3. Documentation

- [x] 3.1 The doc on `StatefulComponent` names the test; no wiki page says which settings a component adopts,
      and none changes
- [x] 3.2 One ledger entry citing #1142, as a file of its own under `docs/ledger/`
