# Tasks

## 1. Measured before

- [ ] 1.1 `PerfHarnessTests` gains a frame after a key typed into a field whose handler calls no
  `SetState`, measured on main's code, the numbers in the pull request

## 2. The text on the position

- [ ] 2.1 `FieldText.Of` and `FieldText.Typed` in `eQuantic.UI.Native.Framework`, over an internal state
  kept on the field's position, declared in `PublicAPI.Unshipped.txt`
- [ ] 2.2 Tests: a field keeps what was typed while the page does not build it, takes the page's value
  when it does (a new node, whether or not its value changed), and a field at another's place starts
  from its own value

## 3. Every reader

- [ ] 3.1 The measure of a `TextEntry` reads the position's text
- [ ] 3.2 The paint of a field and of its placeholder, and the check that ends a composition, read it
- [ ] 3.3 The semantics walk reads it
- [ ] 3.4 `PhotonHost`: `_edited` goes; the caret, the selection, the word and line moves, a click's
  caret, a triple click and the phone keyboard's editing value read it; `Commit` records the key on
  the position, and a key no longer falls back to rebuilding every component
- [ ] 3.5 Tests for each scenario of the `text-entry` requirement, and `InputBuildsWhatItReachedTests`
  for the `element-tree` one: a key typed into a field whose handler calls no `SetState` builds nothing
  and still draws

## 4. Against the real thing

- [ ] 4.1 `PerfHarnessTests` after the change, beside 1.1
- [ ] 4.2 The whole native suite, and the Compiler and Conformance suites through the lock
- [ ] 4.3 The desktop sample typed into in a window: a field whose handler ignores keys shows them

## 5. Documentation

- [ ] 5.1 `docs/FLUTTER-PARITY.md`'s Element tree row
- [ ] 5.2 The wiki's Photon page, "What a frame builds", in its five languages, on the wiki branch named
  like this pull request's
- [ ] 5.3 One entry for the ledger, citing #996, under `docs/ledger/`
- [ ] 5.4 The change archived before the merge
