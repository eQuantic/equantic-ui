# Proposal

Closes #770, a Feature under #195 (One text-editing protocol across hosts), of which it is the phone
half. It is the first blocker between an app built with the SDK and a store: a sign-in, a sign-up or a
search on a phone could not be filled.

## Why

Read from the shells on `main` at 1850922. The desktop shells hand the keyboard to the host:
`Shell.MacOS` and `Shell.Windows` call `PhotonHost.KeyDown` and `PhotonHost.TextInput`. The phone
shells hand it touches only: `PhotonActivity` (Android) and `PhotonViewController` (iOS) call
`PressDown`, `PointerMove`, `PressUp` and `PointerCancel`, and nothing else reaches the host. Neither
has an input connection (no `OnCreateInputConnection` on Android, no text input responder on iOS), so
no on-screen keyboard ever opens, and a `TextEntry`, a code surface or a sheet cell receives no
character.

A phone's keyboard is also not a desktop's. It does not send keys: it reads the text around the caret
and hands back a new state of the field, a word it autocorrected, the word it is suggesting for, a
swipe, dictation. Forwarding its output as `TextInput` and `KeyDown` would lose every one of those, and
the keyboard, whose idea of the field would drift from the host's, would correct the wrong word.

## What Changes

- **The host takes a field's whole new state.** `PhotonHost.EditingValue` is the field being edited as a
  keyboard sees it (`TextEditingValue`: the text, the selection, the composition), and
  `PhotonHost.UpdateEditingValue` takes the keyboard's edit as the field's new state, Flutter's
  `TextInputClient.updateEditingValue`. A new text reaches the app through `TextEntry.OnChanged`, once,
  as typing does. The composition is IN the text, as on the web and in Flutter, and is drawn
  underlined.
- **One session, platform-free, that both phone shells translate.** `TextInputSession` (Flutter's
  `TextInputConnection`) is polled after each frame and says what the keyboard has to do: open, start
  over on another field, close, or take a value the app changed. It remembers the state the keyboard
  has, so an edit is never echoed back to the keyboard that made it, and an app's rewrite always is.
  A code surface and a sheet keep their text to themselves: the keyboard edits them through keys and
  committed text, the desktop's path (`TextInputTarget.Keys`).
- **The keyboard's area is the host's `ViewInsets`** (Flutter's `MediaQuery.viewInsets`). The tree lays
  out above it, the safe area it covers counts once, and a frame laid out in less height than the one
  before scrolls the field being edited into what is left. A field the rising keyboard covered is still
  the one being edited.
- **Android.** The surface view answers the keyboard with an input connection over a mirror of the
  field, described to the keyboard (a password, a single line that ends on Done, a multi-line field).
  The activity lays out with `adjustResize`, measures the keyboard's inset, shows the keyboard when an
  edit begins or the field is tapped again, and hides it when the edit ends. A hardware keyboard's keys
  reach the host as on a desktop.
- **iOS.** An invisible text view at the caret takes the keyboard, and UIKit runs autocorrection,
  suggestions, dictation and composition against it, kept in step with the field by the session. The
  keyboard's frame becomes the host's `ViewInsets`. A hardware keyboard's keys reach the host as on a
  desktop.

For a developer: nothing to write. A `TextEntry`, a `TextInput`, a `SearchField`, a code surface or a
sheet tapped on a phone opens the platform's keyboard, edits the way the platform edits (autocorrect,
suggestions, dictation, a composition), and stays visible above it.

## Capabilities

### New Capabilities

- `phone-keyboard`: how a phone's keyboard edits what the host draws, and how the layout makes room
  for it.

### Modified Capabilities

None.

## Impact

- Photon: `PhotonHost.EditingValue`, `UpdateEditingValue` and `ViewInsets`, and `EndEditing`, which a
  shell now calls when the platform puts the keyboard away; the new `TextEditingValue`,
  `TextInputSession`, `TextInputTarget` and `TextInputChange`. The public surface moves in
  `PublicAPI.Unshipped.txt`, with one break: `PhotonRealizer.Realize` takes the composition to draw
  (`composing`), so its signature goes and a wider one replaces it. The parameter is optional, so source
  that names its arguments compiles unchanged, as all 46 calls in the tree did; an assembly built
  against the old signature is rebuilt.
- The Android shell: `PhotonSurfaceView`, `PhotonActivity`, and the new `PhotonInputConnection` and
  `AndroidKeys`.
- The iOS shell: `PhotonViewController`, and the new `PhotonKeyboard`, `PhotonTextProxy` and `IosKeys`.
- Not reached: eqc, the runtime, the web realizer, the macOS and Windows shells, the SDKs and the
  templates. The developer surface does not move.
- Out of scope: the keyboard's type and autofill hints (#675, which builds on this); a code surface's
  and a sheet's own document on a phone, which would let the keyboard autocorrect them, and stays the
  desktop's keys path.
