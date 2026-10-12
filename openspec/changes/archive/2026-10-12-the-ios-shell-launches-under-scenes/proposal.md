# Proposal

Closes #796, a Bug under #774 (Device capabilities on the phones). It is the first of the two defects
the epic's plan puts ahead of everything else on iOS.

## Why

Apple's TN3187: from the major release after iOS 26, an app built with the latest SDK that does not
use the scene life cycle does not launch. The iOS shell used the old one: `PhotonApp.Run` started
`UIApplication.Main` with `PhotonAppDelegate`, whose `FinishedLaunching` made the `UIWindow` from
`UIScreen.MainScreen.Bounds`, and `PhotonApp.plist` declared no `UIApplicationSceneManifest`.

The issue read this as a future problem, "the day the workload targets the iOS 27 SDK". Measured on a
machine with Xcode 27.0 and the default `ios` workload (26.5.10315), it is today's: the Wallet sample
built for the simulator carries `DTSDKName iphonesimulator27.0`, and its executable's
`LC_BUILD_VERSION` says `sdk 27.0`. The native link runs on the installed Xcode whichever .NET pack
resolves, and the SDK turns the Xcode version check off on purpose. So every app built on Xcode 27 is
already "built with the iOS 27 SDK", and would not launch on iOS 27.

## What Changes

- **The window belongs to a scene.** `PhotonApp.PhotonSceneDelegate`, a `UIWindowSceneDelegate`,
  makes the window on the `UIWindowScene` it is connected to and shows the app's screen in it.
- **The app delegate keeps only what is the process's.** `PhotonAppDelegate` no longer owns a window;
  it answers which delegate a scene gets (`GetConfiguration`), with the type named in C#, where the
  compiler checks it, rather than as a class name in the plist. The configuration is unnamed, so UIKit
  does not look for a plist dictionary it would log an error about.
- **The SDK declares the scene manifest.** `PhotonApp.plist`, which the SDK merges into every iOS
  app's Info.plist, gains `UIApplicationSceneManifest` with one scene.
- **The screen outlives the scene.** The system may disconnect a scene in the background and connect
  a new one when the person comes back. The screen (the controller, its host and its tree) is made
  once and kept for the process, so a reconnected scene shows the same page with its state, as the
  window did under the old life cycle.
- **The background stops the clock.** Leaving for the background pauses the display link, where the
  system refuses GPU work; coming back resumes it and presents a fresh frame.

For a developer: nothing to write, and nothing to change. An app built on Xcode 27 launches on iOS 27.
The URLs a scene is opened with arrive at the scene delegate under this life cycle; reading them is the
deep-link work of #777, which this change unblocks.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `native-app-lifecycle`: gains the iOS requirements: the app runs under the scene life cycle, and it
  keeps its screen while it is away.

## Impact

- **The iOS shell** (`eQuantic.UI.Native.Shell.iOS`): `PhotonApp`, `PhotonViewController`.
- **The native SDK** (`eQuantic.UI.Sdk.Native`): `PhotonApp.plist`.
- **CI**: the `samples` job launches the Wallet sample on an iOS simulator and reads its self-test
  line, where it only built it before.
- **Public surface moves.** `PhotonAppDelegate.Window` goes (`*REMOVED*`), and
  `PhotonAppDelegate.GetConfiguration` and `PhotonApp.PhotonSceneDelegate` arrive. The developer
  surface does not move: no property, setting or template parameter.
- **Migration**: none for an app that runs through `PhotonApp.Run` or the generated `Main`. Code that
  read the window from the app delegate reads it from the connected scene
  (`UIApplication.SharedApplication.ConnectedScenes`).
