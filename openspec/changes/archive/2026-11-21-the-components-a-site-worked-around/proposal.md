# Proposal

Closes #1780, #1785, #1786 and #1787, four sub-issues of the epic #1774 ("Lessons from a real site: what
fails only in the browser is caught by the build and the tests"), and #941, the other half of #1787.

## Why

An app built on the SDK from an empty repository to production wrote its own way around five gaps in
the components, each one found in a browser:

- **A picker drawn without its value.** `new DatePicker(null, day => ChooseDay(day))` emptied its field
  on every redraw while the day stayed chosen underneath, and lived three slices before anyone saw it.
  Nothing said so. And a form that lays its labels out itself showed the picker's label a second time.
- **A photograph framed by hand.** Every round photograph on the site is framed (where its subject
  sits, how close), and `Image` only covers, centred, so the app wrote `FramedImage` and `PhotoFraming`.
- **A canvas a finger could not drag.** The frame editor moves the photograph by dragging on a
  `Canvas`; on a phone the drag scrolled the page and cancelled the pointer stream, so the app added
  arrow buttons.
- **An action's result that could not open.** The back office prepares a catalogue's PDF through a
  server action that answers with an address, and the page could not open it: the screen became two
  steps, a press to prepare and a press on a link.
- **A link that could not open beside the page** (#941): a consent form's links to the terms took the
  person away from what they had filled in.

## What Changes

- **A picker says so.** A `DatePicker`, a `TimePicker` or a `DateTimePicker` that reported a value
  through `onChanged` and is drawn again with none says, in development, that its field emptied and
  that it shows what it is handed. A `DateTimePicker` says it once, for the moment, and not again for
  its halves. The notices go through a new channel, `DevelopmentNotices`, which the browser's
  development runtime writes to the console and a Photon app's host writes to its logger, as a
  warning, in Development. A production page and an app in any other environment arm no channel, and
  a notice is dropped where it is said.
- **A field can leave its label out.** `TextInput.LabelHidden`, `DatePicker.LabelHidden` and
  `DateTimePicker.LabelHidden` (the date's), and the `labelHidden:` of their factories, draw no label
  above the field, which still names itself for assistive technology by it.
- **An image frames its picture.** `Image.FocalPoint` (a `Point` of fractions, the centre by default)
  places the picture's point at the same fraction of the box, and `Image.Zoom` (1 by default, never
  less) scales the picture around it, which keeps the point in place and the box covered. The web
  writes `object-position`, a `scale` around a `transform-origin`, and a frame of the box's size that
  clips the zoomed picture and carries the radius; Photon does the same arithmetic.
- **A canvas that follows a drag keeps a finger's drag.** A `Canvas` with an `OnPointerDown` and an
  `OnPointerMove` writes `touch-action: none` beside the `pointer-events: auto` it already wrote. One
  that only hovers (a chart's tooltip) or only takes a press writes nothing, so a full-width chart does
  not trap the page's scroll. Photon routes the touch to the canvas's region already.
- **A handler opens or downloads what an action answered.** `Navigator.OpenAsync(address)` opens an
  address beside the app (a new tab on the web, the system's browser on Photon) and
  `Navigator.DownloadAsync(address, fileName)` downloads a file under a name, each answering whether it
  did. Only `http` and `https` addresses open. An address relative to the page resolves against it on
  the web and is refused where there is no page to resolve it.
- **A link can open beside the page.** `Link.OpensBeside` (and the `Link(…)` factory's `opensBeside`)
  writes `target="_blank" rel="noopener noreferrer"` on both producers; the router already leaves such
  a link to the browser. On Photon an absolute `http` or `https` destination goes to the system's
  browser and an app route navigates in place.

## Capabilities

### New Capabilities

- `development-notices`: what a component tells the developer while the app runs in development.
- `image-framing`: how an image places and scales its picture inside its box.
- `opening-addresses`: what a page's own code opens beside it, and what it downloads.

### Modified Capabilities

- `links`: a link may open beside the page.
- `raw-pointer`: a canvas that follows a drag keeps a finger's drag.
- `text-entry`: a field may leave its label out of the drawing.

## Impact

- The vocabulary (`Image`, `Link`, `Navigator`, `DevelopmentNotices` in `eQuantic.UI.Primitives`), the
  components (`TextInput`, `DatePicker`, `TimePicker`, `DateTimePicker`, `UI.Link`, and their committed
  transpiled twins), the web realizer (`WebLoweringVisitor`, `HtmlStyle.TransformOrigin`), the runtime
  (`lowering.ts`, `vocabulary.ts`, `nodes.ts`, `navigator.ts`, `development-notices.ts`, its exports),
  the development boot (`boot.dev.ts`), and Photon (`EmitVisitor`, `LinkRegion`, `PhotonHost`,
  `PhotonApplication`).
- Public surface, all additions: `Image.FocalPoint`, `Image.Zoom`, `Link.OpensBeside`,
  `Navigator.OpenAsync`, `Navigator.DownloadAsync`, `DevelopmentNotices` (Primitives); `LabelHidden` on
  `TextInput`, `DatePicker` and `DateTimePicker`, the `labelHidden` parameter of their factories and
  `UI.Link`'s `opensBeside` parameter (Components); `HtmlStyle.TransformOrigin` (Web);
  `LinkRegion.OpensBeside` (Native.Components, which 1.0 does not promise). Four factories gain a
  parameter, so their old signatures leave the declared surface; a call site does not change. The developer surface does not move.
- No break for an app: every default writes what it wrote before (a centred image at zoom 1, a link in
  place, a canvas that follows no drag, a chart's included), and a picker handed back its value says
  nothing.
- Rules followed: 1 (the developer writes C#; the SDK writes `object-position`, `touch-action` and
  `target`), 5 (`FocalPoint`, `Zoom` and `OpensBeside` are no target's words; `TransformOrigin` sits on
  the DOM's escape hatch, which transcribes CSS on purpose), 6 (Flutter's `Image.alignment`, a pan's
  gesture arena and `url_launcher`), 9 (an out-of-range focal point or zoom throws where it is set, as
  .NET's own setters do, and the browser's own `window.open` and anchor download do the opening), 14
  (each gap closes in the SDK, not in the app).
