# Tasks

## 1. The relay

- [x] 1.1 `DeepLinkRelay.StartedWith`: what a start came with, a URL or nothing, replacing the answer on each start; a URL offered later is heard and is not `Launch`. Verify: `DeepLinkRelayTests` (18 cases)

## 2. The declaration

- [x] 2.1 The generator writes the declared schemes as an `[IntentFilter]` on the Android launcher and as `UrlSchemes` in code, and the launcher is a single task when there is a scheme. Verify: `PhotonProgramGeneratorTests`, and the manifest of a built app
- [x] 2.2 `eqicon capabilities` writes `CFBundleURLTypes` into the iOS partial manifest through the writer the Mac's bundle uses, named by `--id`, leaves the file alone when nothing changed, and removes it when nothing is declared. Verify: `PhonePartialManifestTests`, `MacAppPackagingTests`
- [x] 2.3 The manifest is written after `Compile` and handed over before the final manifest is written (#872). Verify: `IosManifestTargetsTests`, `NativeSdkEvaluationTests`, `EveryProgramTheSdksStartTests`, and `scripts/check-ios-launch.sh`, which fails with main's targets naming `NSFaceIDUsageDescription` and `NSPhotoLibraryUsageDescription`

## 3. The shells and the host

- [x] 3.1 `PhotonHost.Unmount`. Verify: `HostUnmountTests`
- [x] 3.2 Android: a create says what the start came with and what kind it is, `onNewIntent` offers, only a declared scheme is a link, a tree is taken down with its activity, and the relay is registered. iOS: the scene's connection and `scene:openURLContexts:`, and the relay is registered. Verify: on the devices below

## 4. The devices

- [x] 4.1 Android emulator (API 36): started by a link, the constructor reads it; a link while it runs is heard, one instance, same process; an undeclared scheme resolves to nothing; started from the icon, `Launch` is null before and after a later link; in one process, a link start and an icon start each read their own
- [x] 4.2 Android emulator, one script for both sides of the review's points: after Back and a relaunch a link is heard once (twice before); an intent aimed at the launcher by name on another scheme is heard by nobody and is not `Launch` (it was both); after the process is killed a return reads null (the old link before); a re-creation for a font size reads the same `Launch` and tells no subscriber
- [x] 4.3 iOS 27 simulator: the built `Info.plist` carries the URL type and the camera sentence; started by a link, the constructor reads it; a link while it runs is heard; started from the icon, `Launch` is null before and after a later link
- [x] 4.4 An iPhone bench app with a `.resx`, built from clean both ways: with the compiler brought forward the assembly has no resource, with the late hand-over it has it, and the `Info.plist` carries the declared keys both ways

## 5. Documentation and the suites

- [x] 5.1 The wiki's Capabilities page, in English and Portuguese, on the wiki branch named like this pull request: the table of shells and the section on links. Verify: `WikiShellRealizationsTests`
- [x] 5.2 `docs/platform-catalog/ios.md`, `docs/PHONE-CAPABILITIES-PLAN.md`, and one `docs/LEDGER.md` line citing #777 and #872
- [x] 5.3 The Native.Engine and Web suites, each alone and read by its exit code
