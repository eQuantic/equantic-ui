# Design

## The picker's notice: said by the component, heard where the developer looks

The issue asked for React's "a component is changing a controlled input to be uncontrolled". The
picker is the only party that knows both halves of the mistake: it reported a value, and the next
configuration hands it none. So the notice is the picker's, in `AdoptConfig`, where the next
configuration arrives: it remembers the value it last reported until its parent draws it again, and says
so when that drawing hands back nothing. It never says anything when nobody chose a value, or when the
value it is handed is the one it reported.

A `DateTimePicker` composes a `DatePicker` and a `TimePicker`, and when the page drops its moment the
halves are handed nothing too. They would each say their own notice for the page's one mistake, naming
a component the page never wrote, so the halves are marked `Composed` (internal) and say nothing.

Where a notice goes is not the component's business. `DevelopmentNotices` is a sink and a `Say`, both
sides alike (the C# class in Primitives, its twin in the runtime), and whoever runs the app in
development arms the sink: the development boot (`boot.dev.ts`, the runtime the server serves in
Development and under hot reload) writes to the console, and `PhotonApplication.Run` writes to the app's
logger, as a warning, when its environment is Development. The production runtime carries no sink and
no code to arm one; `Say` returns at the null check. Flutter says these things through `assert` and
`debugPrint`, which are compiled out of a release build; the sink is our equivalent of that switch,
decided by the runtime the server chose rather than by a compiler flag the transpiler would have to
honour.

## The label: hidden from the drawing, never from assistive technology

`LabelHidden` draws no label above the field and leaves the field's accessible name as it was (the
label), so a form that draws its own label beside the field does not draw it twice and a screen reader
still hears it once. It is backed by a field, so `AdoptConfig` carries it when a page changes it.

## The image's frame: the browser's own rule, computed the same on Photon

Flutter places a picture in its box with `Image.alignment`, and a `FractionalOffset` alignment is
exactly CSS's `object-position` in percentages: the picture's point at that fraction stands at the same
fraction of the box. That rule needs nothing of the picture but the fit, which the browser applies before
the picture loads, so the server writes it and hydration changes nothing. A zoom is a scale around the
same point (`transform-origin`): the point stays, the picture grows, and a picture that covered its box
still covers it, so the frame never shows past the photograph's edge without clamping anything.

A scaled `<img>` overflows its element, so a zoomed picture is wrapped in a frame of the box's size that
clips it and carries the corner radius; an unzoomed one is the markup it always was. Photon computes the
destination rectangle with the same two formulas and clips when it zooms.

## The canvas: claim the gesture a drag's handlers follow

A Flutter `GestureDetector` with a pan wins the gesture arena over the scrollable beneath it; a
`MouseRegion` and a tap do not. The browser's equivalent is `touch-action`: a canvas whose handlers
follow a drag, a press and a move, writes `none`, so a finger's drag reaches them instead of scrolling
the page and cancelling the pointer stream.

The first version claimed the finger for any handler, and the variant matrix showed what that cost: the
`BarChart` draws on a canvas that listens to a move and a leave, for its tooltip, and a full-width chart
would have trapped a phone's scroll. So a canvas that only hovers, only takes a press, or listens to
nothing writes no `touch-action`, and the page scrolls across it as before. Photon routes a touch to the
canvas's region already. A finer choice (`pan-y`, for a chart that follows a horizontal finger) waits for
an app that needs it.

## Opening an address: the navigator, which already knows where it runs

The app had `Navigator.Of(context)` for going somewhere inside the app. Opening beside it belongs to the
same object rather than to a new service: on the web it is `window.open` with the new tab cut from its
opener, inside the time a person's press still allows a page to open one (a handler that awaited an
action is within it); on Photon it is `IUrlLauncher`, the contract every shell registers, under the same
`OpenUrlPolicy`. Flutter's answer is `url_launcher`'s `launchUrl`, a plugin; ours ships with the SDK.

Only `http` and `https` open on the web, so an address a server or a person wrote can never run script
in a tab the page opened. A download of the page's own origin goes through an anchor with `download`,
which the browser names; another origin's file opens in a tab, since a browser ignores the name of a
file it cannot read and would navigate the page away. A relative address resolves against the page on
the web, and is refused on Photon, where there is no page: `/print/1` is a path from a page's root,
though .NET on macOS and Linux reads it as an absolute file URI, so a leading slash is refused before
`Uri` is asked.

## A link beside the page

`OpensBeside` writes `target="_blank" rel="noopener noreferrer"`, in the same order on both producers,
and the router's existing eligibility check already leaves such an anchor to the browser. On Photon a
link region carries the flag; a tap or an activation by id hands an absolute `http` or `https`
destination to the launcher, and navigates in place to an app route, which has no second page to open in.
