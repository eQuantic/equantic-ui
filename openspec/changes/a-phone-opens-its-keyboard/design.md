# Design

## How Flutter solves it

Flutter draws its own fields too, and solves the same problem with three pieces, which this change
takes in the same shape:

- **The state crosses whole.** `EditableText` is a `TextInputClient`: the engine hands it
  `updateEditingValue(TextEditingValue)`, the field's text, selection and composing range, and it hands
  the engine `setEditingState` when the app changes the value. Neither side sends keys for text. Here
  that is `PhotonHost.UpdateEditingValue` and `PhotonHost.EditingValue`, and `TextEditingValue` keeps
  Flutter's name and fields.
- **A connection per edit.** `TextInput.attach` opens a `TextInputConnection` for one client, `show`
  raises the keyboard, `close` ends it, and attaching another client restarts the platform's input.
  Here that is `TextInputSession`, which a shell polls after each frame rather than being called from
  the field, because Photon's focus lives in the host and a frame is where it settles.
- **The platform's own input machinery, mirrored.** On Android, `TextInputPlugin` restarts the
  `InputMethodManager` per client and `InputConnectionAdaptor`, a `BaseInputConnection`, edits a
  `ListenableEditingState` (a `SpannableStringBuilder`) and reports `updateSelection` after every
  change, batch edits included. `PhotonInputConnection` is that shape. On iOS, `FlutterTextInputView`
  implements `UITextInput` itself. Here an invisible `UITextView` does instead; the decision is below.

Making room is `MediaQuery.viewInsets`: the engine reports the keyboard's area, `Scaffold` lays out
above it by default (`resizeToAvoidBottomInset`), and `EditableText.didChangeMetrics` scrolls the caret
into view when the bottom inset grows. Here that is `PhotonHost.ViewInsets`, the layout's height less
its bottom, and a frame laid out shorter than the last revealing the field being edited.

`docs/FLUTTER-PARITY.md` gains a `TextInputClient` row for the protocol (SAME) and a
`MediaQuery.viewInsets` row for the room (SAME). The `TextEditingController` row stays PARTIAL: the
sheet's editing is still the host's, and the web's field is still the browser's own.

## Decisions

**The composition is in the text.** A phone keyboard keeps the word it is suggesting for inside the
field, as the web's `input` and Flutter's `TextEditingValue` do, and the app hears it as it is typed: a
search filters on the word, not on the space after it. The host only marks the range and draws it
underlined. The desktop's marked text, which the host holds OUTSIDE the value until it commits, stays
as it is for a desktop's input method, and a code surface or a sheet on a phone uses it too.

**The composition travels with its text.** The host keeps the range together with the text it was
marked in, and draws it only while the field still holds that text. An app that rewrites the value (a
mask, a length cap) ends the composition at the next frame, and the keyboard is told, rather than an
underline being drawn over characters that are no longer the word.

**The session remembers what the keyboard has.** Every edit the keyboard makes is stored as the state
the keyboard has, so the next poll reports nothing when the app took it as it came. When the app took
something else (a read-only field, a line break flattened, its own rewrite), the poll reports a value
change and the shell tells the keyboard. A keyboard told its own edit back restarts its composition,
which is the failure this avoids.

**Fields are documents; a code surface and a sheet are keys.** A code surface and a sheet keep their
own text and caret models (`CodeEditorController`, `SheetController`), which a keyboard's document
would have to be translated into and out of. They take the desktop's path, keys and committed text,
and the Android keyboard is described to them as `TYPE_NULL`, the type for an editor that is not a
document. Autocorrection in code would be a defect anyway.

**iOS edits a hidden text view rather than implementing `UITextInput`.** Flutter's
`FlutterTextInputView` is the alternative: a responder implementing the whole protocol, positions,
ranges, a tokenizer, selection and marked-text geometry, several thousand lines in the engine, each one
a place where the system's own behaviour has to be reproduced. A `UITextView` already implements it as
the system means it, autocorrection and dictation included, and the session keeps it in step with the
field the same way the Android mirror is kept. It is invisible (alpha 0, which keeps it out of hit
testing and still takes the keyboard), hidden from accessibility, one clipped line, and stands at the
caret so the system's candidate bar and dictation appear at the field. The price is a second copy of
the field's text, which the session already accounts for.

**The reveal follows the layout, not the keyboard's event.** A field is revealed when a frame lays out
shorter than the one before, whatever made it shorter: the keyboard, a split screen, a rotation. A
field the keyboard covered has no text region left in that frame, so the host finds it by its focus
stop, which a frame keeps for every focusable thing whether or not its viewport still reaches it, and
the edit is not lost to the layout that was meant to show it.

**Android measures the keyboard itself.** With `adjustResize` the window may or may not shrink,
depending on the version and on whether the app draws edge to edge. From API 30 the shell reads the
IME's inset and keeps only the part that overlaps the view, so a window that already shrank takes no
inset twice; before API 30 it reads the stable insets. The keyboard is shown and hidden through the
window's insets controller from API 30, and through the `InputMethodManager` before it.

## Risks

- The emulator's and the simulator's keyboards are measured here; another vendor's (Samsung's, a
  third-party swipe keyboard) may batch or compose differently. The Android connection follows
  `BaseInputConnection`'s contract and reports every change, which is what such keyboards rely on.
- On iOS 17 and later, inline prediction is turned off on the proxy: it writes the predicted word into
  the text view, greyed, as text the field would never show and the app would never hold.
- A code surface and a sheet on a phone get no autocorrection and no suggestions, by design, and on
  Android a keyboard that composes for a `TYPE_NULL` editor anyway reaches the host as marked text.
