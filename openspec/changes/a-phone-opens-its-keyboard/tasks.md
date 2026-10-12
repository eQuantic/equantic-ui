# Tasks

## 1. The editing value in the host

- [x] 1.1 `TextEditingValue`, `PhotonHost.EditingValue` and `PhotonHost.UpdateEditingValue`: the keyboard's edit is the field's new state, handed to the app once, a single line flattened with its selection kept on its text, a disabled or absent field refused. Verify: `PhoneKeyboardTests`, the editing value cases, `AFlattenedLineBreak_KeepsTheSelectionOnItsText` failing without its fix
- [x] 1.2 The composition is in the text, drawn underlined, and ends with a caret the host moves or a text the app rewrites. Verify: `PhoneKeyboardTests.AComposition_IsDrawnUnderlined_AcrossItsRange` and `AnAppThatRewritesTheText_EndsTheComposition`, each failing without its fix
- [x] 1.3 `TextInputSession`, `TextInputTarget` and `TextInputChange`: open, start over, close, a value the app changed, never an echo; an edit that arrives after the caret left its field goes nowhere; a code surface and a sheet are keys, under the keyboard too; a field the app takes away or disables closes the keyboard. Verify: `PhoneKeyboardTests`, the session cases, `AnEditThatArrivesAfterTheCaretMoved_GoesNowhere` and `AFieldTheAppTakesAway_PutsTheKeyboardAway` failing without their fixes, and `ACodeSurfaceTheKeyboardCovers_StillTakesTheKeys` failing when only the frame's code and sheet regions are read
- [x] 1.4 `PhotonHost.ViewInsets`: the tree lays out above the keyboard, the safe area under it counts once, a shorter frame reveals the field being edited, and a covered field is still the one being edited. Verify: `PhoneKeyboardTests`, the insets cases, each failing without its fix, and the Native.Engine suite whole
- [x] 1.5 Where nothing scrolls, the host lifts the screen until the field being edited clears the keyboard, takes presses where it is drawn and reports the caret and the semantics there. Verify: `PhoneKeyboardTests.AFieldNothingScrolls_IsLiftedAboveTheKeyboard`, `AFieldAShorterWindowCovers_IsLiftedToo`, `AFieldAScrollViewReveals_IsNotLifted`, `AFieldScrolledUnderTheKeyboard_IsNotLifted` and `AWindowThatWasAlwaysTooShort_IsNotLifted`, each failing without its fix
- [x] 1.6 `TextInputSession.Submit`: the action key submits the field and ends the edit. Verify: `PhoneKeyboardTests.TheActionKey_SubmitsTheField_AndEndsTheEdit`, failing without it

## 2. The Android shell

- [x] 2.1 `PhotonInputConnection` over a mirror of the field, described to the keyboard; `PhotonSurfaceView` answers the keyboard and reports the window's insets; `AndroidKeys` names a hardware keyboard's keys. Verify: `dotnet build src/eQuantic.UI.Native.Shell.Android` on the Mac, with no new warning
- [x] 2.2 `PhotonActivity` opens, restarts and closes the keyboard with the session, lays out with `adjustResize`, and gives the host the keyboard's inset. Verify: on the emulator (API 36), the Wallet's PIX key field opens the keyboard above it; on-screen typing reaches it; autocorrection ends in the field; Backspace works and Done puts the keyboard away; back hides the keyboard, a tap shows it, back again ends the edit; and in landscape the screen lifts so the field stays above the keyboard
- [x] 2.3 A keyboard that composes reaches the field with its composition underlined. Verify: on the emulator, with Gboard's Japanese layout (its English layout commits every letter, as it does in a native `EditText`): "ka" arrives as `setComposingText` and is drawn underlined, a candidate picked replaces it without the underline, and a composition after committed text underlines only itself
- [ ] 2.4 `AndroidClipboard` over `ClipboardManager`, given to the host and registered as `ITextClipboard`. Verify: on the emulator, a field's text selected, cut and pasted back from an attached keyboard (Ctrl+A, X, V), the cut emptying the field and the paste restoring it, where without a clipboard the cut text was lost; a Markdown code block's Copy button writing to the same clipboard; and `dotnet build src/eQuantic.UI.Native.Shell.Android` with no new warning

## 3. The iOS shell

- [x] 3.1 `PhotonTextProxy`, `PhotonKeyboard` and `IosKeys`; `PhotonViewController` syncs the keyboard after each frame. Verify: `dotnet build src/eQuantic.UI.Native.Shell.iOS` on the Mac, with no new warning, and the `samples` job building the Wallet's iOS head
- [x] 3.2 Verify: on the iOS simulator (iPhone 17, iOS 26.5), the Wallet's PIX key field opens the keyboard above it; on-screen typing reaches it; autocorrection ends in the field; Backspace works; Done puts the keyboard away and ends the edit, and a tap brings both back with the caret where it was; and with the Japanese Romaji keyboard, "ka" is drawn underlined and a candidate picked replaces it without the underline
- [ ] 3.3 `IosClipboard` over the general `UIPasteboard`, given to the host and registered as `ITextClipboard`. Verify: on the simulator with its hardware keyboard, ⌘A, ⌘X and ⌘V in the PIX key field, the cut text on the pasteboard (`xcrun simctl pbpaste`) and pasted back; a Markdown code block's Copy button writing to the pasteboard; and `dotnet build src/eQuantic.UI.Native.Shell.iOS` with no new warning

## 4. Documentation and the suites

- [x] 4.1 The wiki's Photon page (EN + pt-BR), in one commit on the wiki branch named like this one
- [x] 4.2 `docs/FLUTTER-PARITY.md`'s `TextInputClient` and `MediaQuery.viewInsets` rows with their probes, and one `docs/LEDGER.md` line citing #770
- [x] 4.3 The suites, each alone and read by its exit code: Native.Engine, Web, Compiler, Server and the runtime's `TestRuntime`
- [ ] 4.4 The wiki's Capabilities page lists the phones for `ITextClipboard`, and the Photon page says what an attached keyboard copies through (EN + pt-BR), on the same wiki branch
