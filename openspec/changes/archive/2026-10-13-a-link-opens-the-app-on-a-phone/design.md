# Design

## How Flutter solves it

Flutter's engine hands the launch link to the framework as the initial route and later ones as
route pushes, when the app opts in (`FlutterDeepLinkingEnabled` in the Info.plist, a `flutter_deeplinking_enabled`
meta-data element in the manifest). The scheme itself is the developer's to write: `CFBundleURLTypes`
in `Info.plist` and an `<intent-filter>` in `AndroidManifest.xml`, by hand, in two languages. Here
the scheme is one C# statement the build turns into both, which is the `MethodChannel` row of
`docs/FLUTTER-PARITY.md` seen from the declaration side.

## Decisions

**The scheme travels the road each system already has.** On iOS that is the partial manifest the SDK
writes from the compiled assembly, where the permission sentences already go. On Android there is no
manifest writer to extend: .NET for Android builds the manifest from attributes, so the filter is an
`[IntentFilter]` on the launcher the generator already writes. Two mechanisms, because the two
platforms have two, and one declaration feeding both.

**A relay is told what the app was started with.** The relay took the first URL it ever saw for the
launch. That is the best the Mac can do, where a launch link and a later one are the same AppleEvent,
and it is wrong on a phone, where the shell knows: an app opened from its icon answered `Launch` with
a link tapped an hour later. `StartedWith` says it, a URL or nothing, and replaces the answer on each
start, since a phone keeps a process and starts the app in it again. The desktop shells go on
offering, and making them exact is #874.

**Before the first tree.** Android says it in `onCreate`, iOS as the scene connects and before the
screen is built, so the root's constructor already reads `Launch`. On macOS that is still not so, and
the contract keeps saying to subscribe as well.

**A start, or a link to a running app.** The rule is whether the tree is rebuilt. Android builds a
new one on every `onCreate`, so each create is a start. iOS keeps the screen for the process, so only
the first connection is a start, and a scene that reconnects with a link is a link to a running app.
Coming to the front is not a start on any of them: the icon tapped while an Android app runs reaches
the single task as the launcher's own intent, the tree is alive, and `Launch` stays what it was, as
it does on an iPhone and on a Mac.

**One activity, one task, for an app that links open.** By default a link opened by another app
starts the activity it names, which is a second instance over the running one: a second surface, a
second tree from the app's root, and `PhotonActivity.Current` pointing at it. As a single task the
running activity is handed the intent, in `onNewIntent`. It has a price: re-entered from its icon, a
single task ends what it had opened on top of itself, a photo picker among them. So only an app that
declares a scheme pays it, and one that declares none keeps the launch mode it had.

**Only a declared scheme is a link.** The launcher is exported, and an intent aimed at it by name is
not held against the manifest's filter, so another app could hand this one any URL as its `Launch`.
The Apple shells are only ever given what the app registered for. The generator writes the schemes
onto the launcher as code (`UrlSchemes`), where trimming cannot take them, and the shell holds each
intent against them.

**A create says what kind it is.** Android hands every create the intent that first started the
activity. A start reads it. A return from the recent apps carries the old intent marked as coming
from history, and the person opened the app, not the link again: a start with nothing. A re-creation
in a live process is not a start at all, and the relay is left as it was.

**A tree is taken down with its activity.** Android ends an activity and keeps the process, and
nothing unmounted a tree: the components of every activity the process had stayed subscribed to its
services. Measured on the emulator, a link opened after one Back and relaunch was heard twice.
`PhotonHost.Unmount` tells every component that mounted that it left, the shell calls it from
`onDestroy`, and a new activity takes down the one before it first, since Android may destroy that
one seconds later.

**The partial manifest is handed over late** (#872). Apple's packaging compiles a first manifest
early in the build and merges what later steps contribute as it writes the bundle's `Info.plist`.
Added to the early list after `Compile`, as it was, the file was written and never merged. The early
list has a public door, `CollectAppManifestsDependsOn`, and it is the wrong one: a target on it that
reads the compiled assembly drags the compiler in front of the rest of the build, and an app's
`.resx` is then no longer embedded (measured, and found by review after this change had gone that
way). So the file is written after `Compile` and added to the late list, `_PostCompilePartialAppManifest`,
before `_WriteAppManifest`. Those names are the iOS SDK's own, and the check below is what fails if a
release renames them. A build of several runtime identifiers writes the final manifest in an outer
build that compiles nothing, so the hand-over is its own target. The file is rewritten only when it
changed, and with nothing declared it is removed: the SDK hands it over whenever it exists.

**Verified links are their own change.** An `https` link the app owns needs the site's word (a hosted
file) and the app's claim (an entitlement on iOS, a verified filter on Android), and on iOS arrives
as a user activity, not a URL context. That is #873.

## What is proved where

`DeepLinkRelayTests` holds the relay's new rule. `PhotonProgramGeneratorTests` reads the launcher
the generator writes: the filter with the declared schemes and the same schemes as code, neither
without, and the single task only with them. `HostUnmountTests` holds the unmount.
`PhonePartialManifestTests` holds the iOS partial manifest: the schemes named by the app's
identifier, a file left alone when nothing changed, and a stale one removed.
`IosManifestTargetsTests` and `NativeSdkEvaluationTests` hold the shape of the hand-over, and
`EveryProgramTheSdksStartTests` the tool's arguments.

`scripts/check-ios-launch.sh` compares every key the SDK wrote for the Wallet sample with the built
`Info.plist`, and fails with main's targets naming the two keys that never arrived.

The shells are proved on the devices, with a bench app that prints what it reads. On the Android
emulator (API 36): started by a link, the constructor reads it; a link while it runs is heard by the
one instance; an undeclared scheme resolves to nothing; started from the icon, `Launch` is null and
stays null after a later link; and in one process, a start by a link after a start from the icon and
back again each read their own. With one script for both sides of the review's points: a link after
a Back and relaunch is heard once where it was heard twice, an intent aimed at the launcher by name
on another scheme is heard by nobody where it became `Launch`, a return after the process was killed
reads null where it read the old link, and a re-creation for a font size tells no subscriber. On the
iOS 27 simulator: the same four facts as the first list, and the built `Info.plist` carries the URL
type. An iPhone bench app with a `.resx` keeps the resource in its assembly with the late hand-over,
and loses it with the compiler brought forward.
