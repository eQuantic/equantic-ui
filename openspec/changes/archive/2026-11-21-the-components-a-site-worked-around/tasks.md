# Tasks

## 1. The pickers

- [x] 1.1 `DevelopmentNotices` in Primitives and its twin in the runtime, exported; the development boot writes notices to the console
- [x] 1.2 `DatePicker`, `TimePicker` and `DateTimePicker` remember what they reported and say so in `AdoptConfig` when drawn again with none; a `DateTimePicker`'s halves are `Composed` and say nothing
- [x] 1.3 `LabelHidden` on `TextInput`, `DatePicker` and `DateTimePicker`, carried by `AdoptConfig`
- [x] 1.4 `PhotonApplication.Run` arms the sink with the app's logger in Development
- [x] 1.5 Regenerate the components' committed twins (`EQ_UPDATE_TRANSPILED=1`)
- [x] 1.6 Verify: the tester hears one notice per mistake and none for a picker nobody chose from or one handed back its value; saying it on every redraw, saying nothing, and letting a `DateTimePicker`'s half speak each fail a case; forgetting `LabelHidden` in either `AdoptConfig` fails the redraw case; the dashboard in a browser warns in the console when a `DatePicker(null, …)` is typed into

## 2. The image's frame

- [x] 2.1 `Image.FocalPoint` and `Image.Zoom`, validated where they are set, in C# and in the runtime's vocabulary
- [x] 2.2 The web lowering on both producers: `object-position`, `scale` around `transform-origin`, and a clipping frame for a zoomed picture; `HtmlStyle.TransformOrigin`
- [x] 2.3 Photon's destination rectangle and clip
- [x] 2.4 Verify: the C# and TypeScript class strings are pinned to each other; ignoring the focal point, dropping the frame and ignoring the zoom each fail; the dashboard in a browser draws a 220 px round frame with `object-position: 30% 70%` and `scale(1.5)`

## 3. The canvas

- [x] 3.1 A canvas with an `OnPointerDown` and an `OnPointerMove` writes `touch-action: none` on both producers; one that only hovers or only takes a press writes none
- [x] 3.2 Verify: removing it fails the C# test and the TypeScript parity spec, and claiming the finger for any handler fails the hover and press cases and the `BarChart`'s variant pins; the dashboard in a browser shows `touch-action: none` on a canvas that follows a drag

## 4. Opening and downloading

- [x] 4.1 `Navigator.OpenAsync` and `DownloadAsync` in C#, through `IUrlLauncher`; `openAsync` and `downloadAsync` in the runtime's navigator
- [x] 4.2 Verify: a launcher receives absolute addresses, a server's render answers false, a relative address and an empty one are refused; the runtime cuts the tab from its opener, refuses every scheme but `http` and `https`, downloads its own origin's file under its name and opens another's in a tab, and each of those fails when removed; the dashboard in a browser opens the address a server action answered with, in a new tab, from the handler that awaited it

## 5. A link beside the page

- [x] 5.1 `Link.OpensBeside` and `UI.Link`'s `opensBeside`, written as `target` and `rel` on both producers
- [x] 5.2 Photon's link region carries it; a tap and an activation hand an address to the launcher and navigate a route
- [x] 5.3 Verify: dropping `target` or `rel` fails the C# test and the TypeScript spec; dropping the check on either Photon path fails its case; the dashboard in a browser writes `target="_blank" rel="noopener noreferrer"`

## 6. Documentation

- [x] 6.1 The ledger's entry under `docs/ledger/`
- [x] 6.2 The wiki's pages in their five languages, on the branch named like this one
