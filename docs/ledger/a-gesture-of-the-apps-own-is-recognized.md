- **2026-11-24 · A gesture of the app's own is recognized in C#, and competes for the pointer with the SDK's
  own in one arena**: an app subclasses `GestureRecognizer`, Flutter's protocol and names (it hears the
  pointers it allows, asks for one with `Resolve`, counts a deadline), and a `RawGestureDetector` hosts it
  through a `GestureRecognizerFactoryWithHandlers<T>`, keeping it across rebuilds and making a new one when the
  factory changes type. The arena and its router are written once in the vocabulary, run by Photon and run
  transpiled in the browser, and one fixture holds both to the same answers; the press, the drags, the sort,
  the context hold and the pans join it as members, keeping their own arming, so a screen with no detector
  behaves as before. On Photon the arena is Flutter's whole, a scroll view's pan included. In the browser the
  scroll is a member that wins by panning: measured in Chromium with real touch input, a hold of 600 ms kept
  every `touchmove` cancelable and the list still, and a swipe at once fired `pointercancel` at 16 px. Each
  pointer event now carries its time, `PointerEvent.TimeMs`. The dashboard's `/gestures` screen and its own
  `HoldThenDrag` show it on both targets. On the web, a detector whose factory changes type at the same place
  keeps its recognizer until an app's construction of a framework generic carries its type argument (#1846)
  ([#1563](https://github.com/eQuantic/equantic-ui/issues/1563)). Proposed and archived through OpenSpec
  (`openspec/specs/gesture-recognizers`).
