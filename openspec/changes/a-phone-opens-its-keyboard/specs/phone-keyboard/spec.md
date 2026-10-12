## ADDED Requirements

### Requirement: A phone's keyboard edits a field as a document

The host SHALL expose the field being edited as a `TextEditingValue` (its text, its selection and the
composition in it) and SHALL take the keyboard's edit as the field's whole new state. A new text SHALL
reach the app through `TextEntry.OnChanged` exactly once per edit; the selection and the composition
SHALL be the host's to keep. A single-line field SHALL flatten a line break into a space. With no
field holding the caret, or a disabled one, the host SHALL have no editing value and SHALL refuse the
edit.

#### Scenario: An edit reaches the app once

- **WHEN** a field holding "hello" takes the keyboard's state "hello world" with the caret at 11
- **THEN** `OnChanged` is called once with "hello world", and the editing value has the caret at 11

#### Scenario: A line break in a single line

- **WHEN** a single-line field takes "one\ntwo"
- **THEN** the app is handed "one two"

#### Scenario: Two edits between frames

- **WHEN** the keyboard sends two edits before a frame is drawn
- **THEN** the second starts from the first, and the app is handed both in order

### Requirement: A composition is part of the value and drawn marked

A composition SHALL be part of the field's text, and its range SHALL be drawn underlined. It SHALL
end when the host moves the caret, and when the app's value is no longer the text it was marked in.

#### Scenario: Composing a word

- **WHEN** the keyboard sends "hel" composing from 0 to 3
- **THEN** the app is handed "hel", and the next frame draws an underline under those three characters

#### Scenario: The app rewrites the text

- **WHEN** the app answers a composing edit by setting the value to something else
- **THEN** the next frame draws no underline, and the editing value has no composition

### Requirement: The keyboard opens, moves and closes with the edit

A shell SHALL learn from a `TextInputSession`, after each frame, whether the keyboard has to open
(an edit began), start over (the caret moved to another field or surface), close (the edit ended) or
take a new value (the app changed it). An edit the keyboard made and the app took as it came SHALL
not be reported back to it. A field SHALL be edited as a document (`TextInputTarget.Text`); a code
surface and a sheet SHALL be edited through keys and committed text (`TextInputTarget.Keys`), and
SHALL stay so while the keyboard clips them. A field the app takes away or disables while it is
edited SHALL close the keyboard (`TextInputTarget.None`).

#### Scenario: Focus opens it and leaving closes it

- **WHEN** a field takes the caret, and later the edit ends
- **THEN** the session reports `Began` after the first frame and `Ended` after the edit ends

#### Scenario: Another field

- **WHEN** the caret moves from one field to another
- **THEN** the session reports `Retargeted`

#### Scenario: No echo

- **WHEN** the keyboard's edit is taken by the app as it came
- **THEN** the next poll reports `None`

#### Scenario: A read-only field

- **WHEN** the keyboard edits a field that has no `OnChanged`
- **THEN** the next poll reports `ValueChanged` with the field's own value

#### Scenario: A code surface

- **WHEN** a code surface holds the caret
- **THEN** the session's target is `Keys` and the host has no editing value

#### Scenario: A code surface under the keyboard

- **WHEN** the keyboard rises over the code surface being edited, and the frame clips it
- **THEN** the next poll reports `None` and the target stays `Keys`

#### Scenario: A field the app takes away

- **WHEN** the field being edited leaves the frame, or is disabled
- **THEN** the next poll reports `Ended` and the target is `None`

#### Scenario: Done

- **WHEN** the keyboard's action key is pressed on a single-line field
- **THEN** the field submits, and the session reports `Ended` after the next frame

### Requirement: The layout makes room for the keyboard

The host's `ViewInsets` SHALL be the part of the window the keyboard covers. The tree SHALL lay out in
the height above it, with the safe area it covers counted once, and a frame laid out in less height
than the one before SHALL scroll the field being edited into view. Where no scroll view holds the field
and the edit has less height than the screen had before it began, the host SHALL draw the screen
lifted by the least that brings the field above the keyboard, SHALL take a press where the screen is
drawn and report the caret and the semantics there, and SHALL draw it in place again when the keyboard
goes down or the edit ends. A field the keyboard covered SHALL stay the one being edited.

#### Scenario: The keyboard rises over a field near the bottom

- **WHEN** a field at the bottom of a scrolled form is being edited and `ViewInsets` gains a bottom
  that covers it
- **THEN** the next frame lays out above the inset with the field inside it, and typing still reaches
  that field

#### Scenario: A field nothing scrolls

- **WHEN** a field laid out at 368 on a screen 600 tall that does not scroll is being edited, and the
  keyboard covers the bottom 300
- **THEN** the caret is drawn above 300, a press where the field is drawn moves its caret, and the
  field is drawn at 368 again once the keyboard goes down

#### Scenario: A window shortened for the keyboard

- **WHEN** the same field is being edited and the window is shortened to 300 instead, with no inset, as
  Android before 15 does for an app not drawn edge to edge
- **THEN** the caret is drawn above 300, and the field is drawn in place again once the window is 600
  tall again

#### Scenario: A field a scroll view brought into view

- **WHEN** a field low in a scrolling form is being edited and the keyboard rises
- **THEN** the form scrolls it into view, and the screen is not lifted

#### Scenario: A field the person scrolls under the keyboard

- **WHEN** a field in a scrolling form is being edited with the keyboard up, and the person scrolls it
  down under the keyboard
- **THEN** the field stays where it was scrolled, and the screen is not lifted

#### Scenario: A window that was always too short

- **WHEN** a field laid out below the bottom of a window that does not scroll is focused, with no
  keyboard and no change in the window's height
- **THEN** the screen is not lifted

#### Scenario: The safe area under the keyboard

- **WHEN** a window 600 tall has a safe area with a bottom of 34, and the keyboard covers its bottom 300
- **THEN** a `SafeArea`'s content ends at 300, where the keyboard begins, not 34 above it

### Requirement: The phone shells open the platform's keyboard

The Android and iOS shells SHALL open the platform's on-screen keyboard while a field, a code surface
or a sheet is being edited, SHALL describe the field to it (a password is not learned or suggested, a
single line ends on the keyboard's action key), SHALL report the keyboard's area to the host, and SHALL
close the keyboard when the edit ends. Autocorrection, suggestions, dictation and composition SHALL
reach the field. A key from a hardware keyboard SHALL reach the host as it does on a desktop.

#### Scenario: Typing into the Wallet's PIX key field on Android

- **WHEN** the PIX key field of the Wallet sample is tapped on the Android emulator and "teh " is typed
  on the on-screen keys
- **THEN** the keyboard opens with the field and the Continue button above it, each letter reaches the
  field as it is typed, and the field ends with what the keyboard's autocorrection chose

#### Scenario: Composing on Android

- **WHEN** "ka" is typed into the PIX key field on the Android emulator with Gboard's Japanese layout,
  and a candidate is picked
- **THEN** the field shows "か" underlined while it is composed, and the candidate replaces it without
  the underline

#### Scenario: Back on Android

- **WHEN** back is pressed once with the keyboard open, the field is tapped, and back is pressed twice
- **THEN** the first back hides the keyboard and the field keeps its caret, the tap shows it again, the
  next back hides it, and the last one ends the edit

#### Scenario: Typing on iOS

- **WHEN** the PIX key field of the Wallet sample is tapped on the iOS simulator, "teh " is typed on the
  on-screen keys, and the keyboard's Done key is pressed
- **THEN** the keyboard opens with the field and the Continue button above it, each letter reaches the
  field as it is typed, the field ends with what the keyboard's autocorrection chose, and Done puts the
  keyboard away and ends the edit

#### Scenario: Composing on iOS

- **WHEN** "ka" is typed into the PIX key field on the iOS simulator with the Japanese Romaji keyboard,
  and a candidate is picked
- **THEN** the field shows "か" underlined while it is composed, and the candidate replaces it without
  the underline

### Requirement: A phone's clipboard serves its fields and its pages

The Android and iOS shells SHALL give the host the platform's clipboard (Android's `ClipboardManager`,
iOS's general `UIPasteboard`), so the copy, cut and paste chords of a keyboard attached to the phone
reach the system clipboard as they do on a desktop. The shells SHALL register the same clipboard as the
`ITextClipboard` service, so a page's own Copy button finds it.

#### Scenario: Cut and paste with an attached keyboard

- **WHEN** the text of a field is selected from a keyboard attached to the phone (Ctrl+A on Android,
  ⌘A on iOS), cut (Ctrl+X, ⌘X), and pasted back (Ctrl+V, ⌘V)
- **THEN** the cut empties the field and leaves its text on the system clipboard, and the paste puts it
  back

#### Scenario: A Copy button on a phone

- **WHEN** a component on Android or iOS asks for `ITextClipboard` with `context.GetService`
- **THEN** it gets the platform's clipboard, and what it writes is on the system clipboard
