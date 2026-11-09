# Tasks

## 1. Measured before

- [x] 1.1 `PerfHarnessTests` gains a frame after a key typed into a field whose handler calls no
  `SetState`, measured on main's code, the numbers in the pull request

## 2. The text on the position

- [x] 2.1 `FieldText.Of` and `FieldText.Typed` in `eQuantic.UI.Native.Framework`, over an internal state
  kept on the field's position, declared in `PublicAPI.Unshipped.txt`
- [x] 2.2 Tests: a field keeps what was typed while the page does not build it, takes the page's value
  when it does (a new node, whether or not its value changed), and a field at another's place starts
  from its own value

## 3. Every reader

- [x] 3.1 The measure of a `TextEntry` reads the position's text
- [x] 3.2 The paint of a field and of its placeholder, and the check that ends a composition, read it
- [x] 3.3 The semantics walk reads it
- [x] 3.4 `PhotonHost`: `_edited` goes; the caret, the selection, the word and line moves, a click's
  caret, a triple click and the phone keyboard's editing value read it; `Commit` records the key on
  the position, and a key no longer falls back to rebuilding every component
- [x] 3.5 Tests for each scenario of the `text-entry` requirement, and `InputBuildsWhatItReachedTests`
  for the `element-tree` one: a key typed into a field whose handler calls no `SetState` builds nothing
  and still draws

## 4. Against the real thing

- [x] 4.1 `PerfHarnessTests` after the change, beside 1.1
- [x] 4.2 The whole native suite, and the Compiler and Conformance suites through the lock
- [x] 4.3 The desktop sample's own form typed into through its host: a name lands after the one the
  form opened with, and the search field's clear button empties it (`StudioFieldsTests`)

## 5. Documentation

- [x] 5.1 `docs/FLUTTER-PARITY.md`'s Element tree row
- [x] 5.2 The wiki's Photon page, "What a frame builds", in its five languages, on the wiki branch named
  like this pull request's
- [x] 5.3 One entry for the ledger, citing #996, under `docs/ledger/`
- [ ] 5.4 The change archived before the merge
