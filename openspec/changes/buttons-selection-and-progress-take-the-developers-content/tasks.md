## 1. The controls take their content

- [ ] 1.1 `child:` on `Button`, `FormSubmit` and `Chip`, a node, replacing `label:`; the members `Child`, mirrored by the factories, carried by `AdoptConfig`
- [ ] 1.2 `title:` on `Checkbox`, a nullable node, replacing `label:`; the member `Title`
- [ ] 1.3 Each states its look on the box around the content (`BoxStyle.Content`, `Foreground`), and the button and the chip their icon size
- [ ] 1.4 The `Pressable` each returns states no `Label`, so its content names it
- [ ] 1.5 Prove both ways: a string draws the pinned output unchanged, a node with a badge or an icon is drawn in the control's look, and a child that is an app's own component names the control, on Photon (the semantics label) and on the web (the button's accessible name); each case fails with the component's old `Text` or `Label` put back

## 2. A chip's avatar

- [ ] 2.1 `avatar:` on `Chip`, a nullable node, drawn before the child at the chip's icon size and in its text colour
- [ ] 2.2 A selected filter chip draws its check in the avatar's place
- [ ] 2.3 Prove the chip's width does not move when it toggles with an avatar, on Photon and on the web

## 3. A group's heading (on #1916's part)

- [ ] 3.1 `title:` on `RadioGroup`, a nullable node, replacing `label:`, drawn in `Caption` on one line in the muted colour
- [ ] 3.2 The heading names the group through #1916's part, and the `Adjustable` takes no string for it
- [ ] 3.3 Prove a heading that is an app's own component names the group on the web (`aria-labelledby` resolves to its words) and on Photon, and fails without the part

## 4. The tree follows

- [ ] 4.1 Every call site in the components, the samples, the templates, the tests and the wiki's snippets that named a renamed parameter or read a renamed member
- [ ] 4.2 The family's five content entries leave `buttons-selection-and-progress.baseline.txt`; the kept ones stay
- [ ] 4.3 The public API declared in this change's file; the transpiled pins and the parity fixture regenerated, and the variant matrix unchanged

## 5. Documentation

- [ ] 5.1 The wiki's Components page and the Upgrading page's migration lines, in the five languages, on the wiki branch named like this one
- [ ] 5.2 One ledger entry under `docs/ledger/`, citing #1607

## 6. Proof against the real thing

- [ ] 6.1 The full local suites (Web, Server, Native.Engine, Email, Compiler, Conformance), the runtime's `TestRuntime` and the `DefaultUIDashboard` and `PhotonDesktop` samples, and a button with a component child read by the browser's accessibility tree
- [ ] 6.2 Archive with `openspec archive buttons-selection-and-progress-take-the-developers-content --yes` before the merge
