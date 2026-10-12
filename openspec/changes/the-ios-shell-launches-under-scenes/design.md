# Design

## How Flutter solves it

Flutter met the same technote and answered it the same way: from Flutter 3.41 its iOS embedder runs
under the scene life cycle by default. Its `AppDelegate` keeps the process-level duties, the UI moves
to a scene delegate (`FlutterSceneDelegate`), the Info.plist gains an Application Scene Manifest, and
the engine is initialized apart from any scene (`FlutterImplicitEngineDelegate`), so what runs Dart is
not torn down with a window. A new row in `docs/FLUTTER-PARITY.md`'s lifecycle section records it.

Two differences, both on the side of writing less. Flutter migrates an app's own `AppDelegate` with
its CLI because a Flutter app owns that file; a Photon app has no delegate to migrate, since the shell
owns both. And `WidgetsBindingObserver`'s states stay a gap here: the shell now hears foreground and
background from the scene, and still surfaces neither to a component.

## Decisions

**The delegate class is named in C#, not in the plist.** Apple's template names the scene delegate
in `UISceneConfigurations`. That is a string matching a `[Register]` name in another file, which
nothing checks until a launch shows a black screen. `application:configurationForConnectingSceneSession:options:`
answers the same question with `typeof(PhotonSceneDelegate)`, and the plist says only what is the
app's: that it uses scenes, and one of them.

**The configuration is unnamed.** Measured: `new UISceneConfiguration("Photon", role)` connected the
scene and logged, at error level on every launch, "Info.plist contained no UIScene configuration
dictionary (looking for configuration named "Photon")". A named configuration is a lookup key into the
plist; with no name, UIKit looks for nothing.

**The screen lives for the process, the window for the scene.** Under the old life cycle the window
was made once and lived as long as the process. A scene does not: the system disconnects one it has
put in the background when it needs the memory, and connects a new one when the person returns. A
controller made in `WillConnect` would rebuild the tree there and come back on the first page, the
defect #771 fixed on Android. So the controller is made the first time a scene connects (after UIKit
is up, as the root factory requires) and every later scene shows the same one. One scene at a time,
because the manifest declares no support for more.

**The clock pauses rather than stops.** Stopping invalidates the display link, which is the self-test's
end. Pausing keeps it, and resuming invalidates the host so the first tick back presents a frame
rather than trusting what the system kept of the last.

**URL contexts are #777's.** Under this life cycle `application:openURL:options:` is not called; a
scene receives its URLs in `connectionOptions.URLContexts` and `scene:openURLContexts:`. The iOS shell
registers no `IDeepLinks` yet, and feeding a `DeepLinkRelay` with no `CFBundleURLTypes` writer behind
it would deliver nothing. The scene delegate documents where they arrive, and #777, now marked as
blocked by #796, reads them there.

## What CI can prove, and what it cannot yet

The `macos-26-arm64` runner image (20260907) carries Xcode 26.0 to 26.6 and iOS runtimes up to 26.5,
with no Xcode 27 and no iOS 27 runtime. So the `samples` job proves the scene life cycle on the newest
simulator the image has: the Wallet sample built in Release, launched with
`Photon__MaxFrames`, its self-test line read from the unified log, and the scene manifest read from the
built Info.plist. That catches a shell that stops connecting its scene, which is this defect's shape
on any runtime.

The iOS 27 half (built on Xcode 27, launched on an iOS 27 runtime, failing on `main` and launching here)
is proved on a developer machine and recorded in the pull request, until the runner image ships
Xcode 27.
