# Tasks

## 1. The editing value in the host

- [x] 1.1 `TextEditingValue`, `PhotonHost.EditingValue` and `PhotonHost.UpdateEditingValue`: the keyboard's edit is the field's new state, handed to the app once, a single line flattened, a disabled or absent field refused. Verify: `PhoneKeyboardTests`, the editing value cases
- [x] 1.2 The composition is in the text, drawn underlined, and ends with a caret the host moves or a text the app rewrites. Verify: `PhoneKeyboardTests.AComposition_IsDrawnUnderlined_AcrossItsRange` and `AnAppThatRewritesTheText_EndsTheComposition`, each failing without its fix
- [x] 1.3 `TextInputSession`, `TextInputTarget` and `TextInputChange`: open, start over, close, a value the app changed, never an echo; a code surface and a sheet are keys. Verify: `PhoneKeyboardTests`, the session cases
- [x] 1.4 `PhotonHost.ViewInsets`: the tree lays out above the keyboard, the safe area under it counts once, a shorter frame reveals the field being edited, and a covered field is still the one being edited. Verify: `PhoneKeyboardTests`, the insets cases, each failing without its fix, and the Native.Engine suite whole

## 2. The Android shell

- [ ] 2.1 `PhotonInputConnection` over a mirror of the field, described to the keyboard; `PhotonSurfaceView` answers the keyboard and reports the window's insets; `AndroidKeys` names a hardware keyboard's keys. Verify: `dotnet build src/eQuantic.UI.Native.Shell.Android` on the Mac, with no new warning
- [ ] 2.2 `PhotonActivity` opens, restarts and closes the keyboard with the session, lays out with `adjustResize`, and gives the host the keyboard's inset. Verify: on the emulator (API 36), the Wallet's PIX key field opens the keyboard above it; on-screen typing reaches it, underlined while composing; autocorrection ends in the field; Backspace and Done work; back hides the keyboard, a tap shows it, back again ends the edit; and in landscape the field stays above the keyboard

## 3. The iOS shell

- [ ] 3.1 `PhotonTextProxy`, `PhotonKeyboard` and `IosKeys`; `PhotonViewController` syncs the keyboard after each frame. Verify: `dotnet build src/eQuantic.UI.Native.Shell.iOS`, and the `samples` job building the Wallet's iOS head
- [ ] 3.2 Verify: on the iOS simulator, the Wallet's PIX key field opens the keyboard above it, on-screen typing reaches it, and the keyboard closes when the edit ends

## 4. Documentation and the suites

- [ ] 4.1 The wiki's Photon page (EN + pt-BR), in one commit on the wiki branch named like this one
- [x] 4.2 `docs/FLUTTER-PARITY.md`'s `TextInputClient` and `MediaQuery.viewInsets` rows with their probes, and one `docs/LEDGER.md` line citing #770
- [ ] 4.3 The suites, each alone and read by its exit code: Native.Engine, Web, Compiler, Server and the runtime's `TestRuntime`
