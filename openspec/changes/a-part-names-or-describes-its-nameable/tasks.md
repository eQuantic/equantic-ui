## 1. The vocabulary

- [x] 1.1 `LabelPart` and `DescriptionPart` in `eQuantic.UI.Primitives`: `SingleChildNode`s, node kinds `labelPart` and `descriptionPart`, their visitor methods, their child slots, and the API declared in this change's file
- [x] 1.2 `SemanticNode.Description`, appended
- [x] 1.3 The two factories in `UI`, and the runtime's twins (`vocabulary.ts`, `nodes.ts`, the node kinds, the exports, the types fixture)
- [ ] 1.4 Check: the coverage suites that enumerate the vocabulary by reflection (closed hierarchy, layout transparency, the Flutter parity probe, the email refusal parity) take the two nodes, each with the answer it gives

## 2. The web

- [x] 2.1 `NamingParts` and `WebLoweringVisitor.Settle`: every node marks the two lists before its subtree and settles after it; an `Overlay` and an anchored panel are scopes; a part's host takes its id only when it reaches a nameable; the nameable lists it in `aria-labelledby` or `aria-describedby`
- [x] 2.2 The browser's twin (`naming-parts.ts`, `settleParts` in `lowering.ts`), with lists of its own per walk (`inPartsWalk`), and a walk that lowers components again alone keeping the ids of the parts left waiting
- [x] 2.3 A control that carries its own label is claimed from the start, on all three producers
- [x] 2.4 The harness (`AnnouncedTree`) takes an `aria-labelledby` only when the text it gathers is not empty, and reads `aria-describedby` into `Announcement.Description`
- [ ] 2.5 Check: the component-parity fixture holds the two producers to the same markup for a dialog, a field, a group and a checkbox, and the runtime's spec reads the names and descriptions, the refusal and the relowered part's id in the browser's lowering

## 3. Photon and email

- [x] 3.1 Measure and emit pass through both parts; the semantics walk wraps them
- [x] 3.2 `NamedByParts` settles the frame before a stop is announced; the dialog, the dialog panel, the group, the live region, the text entry and the checkbox, radio and switch take their name and description from it, over their own label, and a text entry's own description first
- [x] 3.3 The email walk draws the part's child
- [ ] 3.4 Check: `SemanticsParityTests` gains the cases of the spec, read on both targets with their descriptions, and each fails without the change; the refusal is the same sentence on the web, in the browser and on Photon

## 4. Documentation

- [ ] 4.1 The Flutter parity rows for `namesRoute` and `InputDecoration`'s merged label and helper
- [ ] 4.2 The wiki's accessibility page in every language it is written in, with a snippet `WikiClaimsCompile` compiles
- [ ] 4.3 One ledger entry citing #1916, as a file of its own under `docs/ledger/`

## 5. Proof against the real thing

- [ ] 5.1 The full local suites (Web, Server, Native.Engine, Email, Compiler, Conformance), the runtime's `TestRuntime`, and the `DefaultUIDashboard` and `PhotonDesktop` samples
- [ ] 5.2 Archive the change into `screen-reader-parity` with `openspec archive a-part-names-or-describes-its-nameable --yes` before the merge
