Pull request C of the masked field (#1966). It needs `mask-engine-held-to-dotnet` merged, and runs beside B. It is archived after B. A section ends with the check that proves it against the real thing. Its heading names the sub-issue it closes. The shared design is `mask-engine-held-to-dotnet`'s, cited by decision number.

## 10. A formatter decides every edit of a text entry on Photon (5 points, #1977)

- [ ] 10.1 `PhotonHost` asks the entry's formatter at every funnel before `Commit` (design decision 5's table).
  `SetMarkedText` composes unformatted, and the commit is one typed edit.
  - Test: `PhotonFormatterTests` in the native tests, crossing a frame after each key (PROVING §10), with a test
    formatter and the same cases as 8.1 and 8.2.
- [ ] 10.2 `UpdateEditingValue` derives one `TextEdit` from the whole new value, keeps a composing value as sent,
  and the session hands the keyboard the formatter's answer.
  - Test: `PhotonFormatterTests.APhonesWholeValue_ReachesTheFormatterAsOneEdit`, with a digit, a Backspace, a
    selection replaced, an autocorrected word, and a composition then its commit.
- [ ] 10.3 The commands in `EditKey`, the block caret, and an obscured entry drawn `•` only where `IsLiteral` says
  no (`EmitVisitor.Text`).
  - Test: `PhotonFormatterTests` for the commands, and a golden of the obscured field with an assertion of its
    glyphs beside it (PROVING §8).
- [ ] 10.4 Check against the real thing: the PhotonDesktop sample on macOS, with typing, a dead key, a paste,
  `Cmd+Z` and an obscured field.

## 11. The shells take the formatter's keyboard and undo (8 points, #1978)

- [ ] 11.1 `TextInputSession` carries whether the field is prose. `PhotonKeyboard` (iOS) and
  `PhotonInputConnection` (Android) open a non-prose field with no autocorrection, suggestions or capitals.
  - An obscured mask of digits is `NumberVariationPassword` on Android.
  - Test: the session's unit tests.
- [ ] 11.2 The macOS shell's main menu gains Edit › Undo and Redo, sent to the host as the commands while a text
  target holds the caret.
  - Test: the shell's menu test, and the probe that reads the menu's state.
- [ ] 11.3 iOS: `PhotonKeyboard`'s proxy answers `undoManager` with an `NSUndoManager` whose actions call the host,
  so the shake and the three-finger gestures reach the formatter.
  - Android: `KEYCODE_INSERT` and `Ctrl+Z` from a hardware keyboard reach `EditKey`.
  - Windows: `Ctrl+Z`, `Ctrl+Y` and `Insert`.
  - Test: each shell's input tests.
- [ ] 11.4 Check against the real thing, each with a screenshot and the field's text read back from the host:
  - the Windows runner (`gh workflow run ci.yml --ref <branch>`, with the Windows job), for `Insert` and
    `Ctrl+Z`;
  - the iOS 27 simulator (XCUITest), for a postal code's `NumberPad`, a plate's keyboard with no suggestion bar,
    the secure keyboard of an obscured field, and Device › Shake undoing;
  - the Android emulator (`adb shell input`), for `ClassNumber`, no suggestions and `NumberVariationPassword`.
- [ ] 11.5 The wiki's Photon text input page, in the five languages, says what a formatter changes on each shell
  (the keyboard, the Edit menu, the undo gestures). A ledger entry under `docs/ledger/` cites #1966.
  - Test: the wiki guards against the change's wiki branch, and `LedgerTests`.
- [ ] 11.6 Archive the change with `openspec archive a-formatter-decides-a-photon-fields-edits --yes` before its pull
  request merges, after `a-formatter-decides-a-web-fields-edits` is archived: this change MODIFIES the text-entry
  requirements that one adds.
  - Check: `./scripts/check-openspec.sh`.
