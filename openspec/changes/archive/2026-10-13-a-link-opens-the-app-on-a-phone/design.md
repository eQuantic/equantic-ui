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

**One activity, one task.** By default a link opened by another app starts the activity it names,
which is a second instance over the running one: a second surface, a second tree from the app's
root, and `PhotonActivity.Current` pointing at it. As a single task the running activity is handed
the intent, in `onNewIntent`. It is set whether or not the app declares a scheme, since nothing here
is built to be two.

**The manifest target runs where the iOS SDK collects manifests** (#872). Apple's packaging compiles
the app's manifest early and names `CollectAppManifestsDependsOn` as the place for a target that
contributes a partial one. The target depends on `Compile`, whose output it reads, and MSBuild
orders the two. With nothing declared the file is removed: the SDK merges it whenever it exists.

**Verified links are their own change.** An `https` link the app owns needs the site's word (a hosted
file) and the app's claim (an entitlement on iOS, a verified filter on Android), and on iOS arrives
as a user activity, not a URL context. That is #873.

## What is proved where

`DeepLinkRelayTests` holds the relay's new rule. `PhotonProgramGeneratorTests` reads the launcher
the generator writes: the filter with the declared schemes, none without, and the single task.
`PhonePartialManifestTests` holds the iOS partial manifest: the schemes named by the app's
identifier, and a stale file removed. `NativeSdkEvaluationTests` pins where the manifest target is
hooked, and `EveryProgramTheSdksStartTests` the tool's arguments.

`scripts/check-ios-launch.sh` compares every key the SDK wrote for the Wallet sample with the built
`Info.plist`, and fails with main's targets naming the two keys that never arrived.

The shells are proved on the devices, with a bench app that prints what it reads. On the Android
emulator (API 36): started by a link, the constructor reads it; a link while it runs is heard by the
one instance; an undeclared scheme resolves to nothing; started from the icon, `Launch` is null and
stays null after a later link; and in one process, a start by a link after a start from the icon and
back again each read their own. On the iOS 27 simulator: the same four facts, and the built
`Info.plist` carries the URL type.
