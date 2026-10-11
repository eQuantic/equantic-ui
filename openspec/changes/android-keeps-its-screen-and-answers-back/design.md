# Design

## How Flutter solves it

Flutter keeps its engine, and the widget tree with it, when Android takes the surface away: the
`FlutterView` detaches from the surface and reattaches to the next one. That is what #771 does with the
host. `docs/FLUTTER-PARITY.md`'s `WidgetsBindingObserver` row stays a gap: the states are not surfaced
to a component by this change.

Flutter routes Android's back to `Navigator.maybePop()`: the topmost ROUTE pops, a dialog and a bottom
sheet being routes, and a `PopScope` lets a screen refuse or take it. Escape is a different thing
there, a `DismissIntent` the same layers answer. Photon has no route stack: `Navigator.Go` moves to a
destination, and an app's screens are its own state. What it does have is the Escape binding, which
every layer that back should close already declares, and which mounting is the subscription to. So
back runs the Escape binding, and a screen with a back of its own declares it as Escape. A new row in
`docs/FLUTTER-PARITY.md` records the difference.

## Decisions

**The host lives as long as the activity, not as long as the surface.** The surface is what Android
takes away, and it is a drawing resource; the host is the app. Holding the host for the process instead
would also survive back from the first screen, but two activities of one app would then share one host
at two sizes, and nothing here needs that.

**Back is the Escape binding, minus the focus ring.** `KeyDown("Escape")` also clears a focus ring
when nothing else takes the key. On a phone that ring is not drawn, so a back spent on it would be a
press with nothing visible happening; `Back` takes the key only when `CanGoBack` says there is an edit
or a binding.

**Registered only while there is somewhere to go.** Predictive back plays its animation while the
gesture is under the finger, so Android has to know before the gesture whether the app takes it. A
callback registered always would lose the "leave the app" animation, and one that is registered and
takes nothing swallows a back. `CanGoBack` is read after every rendered frame, and a callback that finds
nothing (a frame late) does what the system would: the task goes to the background from its root
activity, and any other activity finishes.

**Both delivery paths, never both for one press.** Android 13 and later deliver back through the
callback when the app opts into predictive back (the default for a target of API 36 on Android 16),
and through `KEYCODE_BACK` otherwise. A platform that calls the callback does not send the key, so
handling both covers every version without a manifest flag.

## Risks

- The activity is still rebuilt by a locale change, which it does not declare: the host follows, and the
  app starts on its first screen. Declaring it needs the culture to follow at run time, which is
  `PhotonCultureController`'s, and is left out.
- A back gesture injected by `adb shell input swipe` did not trigger the system back on the emulator,
  so the gesture is proven by the key, which Android 16 delivers through the same callback.
