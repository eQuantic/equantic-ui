## 1. The typed inputs take a style

- [ ] 1.1 `style:` on `MoneyInput` and `NumberInput`, an `InputTheme`, mirrored by their factories, carried by `AdoptConfig`, handed to the `TextInput` each draws
- [ ] 1.2 Prove it on Photon (`InputsTakeTheirOwnStyleTests`) and on the web (`InputsTakeTheirOwnStyleRealizerTests`, `InputStyleAdoptConfigTests`): each case fails when the style is not handed down

## 2. The parts that need no naming

- [ ] 2.1 `Select(hint:)`, a node, replacing `placeholder:`; the closed field states the field's role and the muted colour for it, and the default is a new localized SDK string in every culture the SDK ships
- [ ] 2.2 `FileUpload(title:, hint:)`, nodes, replacing `label:` and the string `hint:`; the area states the invitation's and the hint's looks
- [ ] 2.3 Prove both ways: a string draws the pinned output unchanged, a node with an icon is drawn inside the part in its look, on Photon and on the web

## 3. The titles, helpers and errors (on #1916's part)

- [ ] 3.1 `title:` on `TextInput`, `FormInput`, `NumberInput`, `MoneyInput`, `DatePicker` and `TimePicker`, `dateTitle:` and `timeTitle:` on `DateTimePicker`; `TitleHidden` replacing `LabelHidden`
- [ ] 3.2 `helper:` and `error:` as nodes on the four fields; the caption line keeps its reserved height and its live region
- [ ] 3.3 The title and the affixes name the entry, and the helper or error describes it, through #1916's part
- [ ] 3.4 Prove a title that is an app's own component names the entry on the web (`aria-labelledby` resolves to its words) and on Photon (the semantics label), and fails without the part

## 4. The tree follows

- [ ] 4.1 Every call site in the samples, the templates, the tests and the wiki's snippets that named a renamed parameter
- [ ] 4.2 The family's content entries and the two `style` entries leave `inputs-and-pickers.baseline.txt`; the kept ones stay
- [ ] 4.3 The public API declared in this change's file; the transpiled pins, the string catalog's twin and the parity fixture regenerated, and the variant matrix unchanged

## 5. Documentation

- [ ] 5.1 The wiki's Components page and the Upgrading page's migration lines, in the five languages, on the wiki branch named like this one
- [ ] 5.2 One ledger entry under `docs/ledger/`, citing #1609

## 6. Proof against the real thing

- [ ] 6.1 The full local suites (Web, Server, Native.Engine, Compiler, Conformance), the runtime's `TestRuntime` and the `DefaultUIDashboard` sample, and a field with a component title read by the browser's accessibility tree
- [ ] 6.2 Archive with `openspec archive inputs-and-pickers-take-the-developers-content --yes` before the merge
