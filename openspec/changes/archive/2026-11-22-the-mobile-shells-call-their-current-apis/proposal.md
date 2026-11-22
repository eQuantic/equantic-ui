# Proposal

Closes #1592, a Bug under #774 (Track D: a device's capabilities in C#).

## Why

Every build of the two mobile shells printed CA1422: eleven calls, on an iOS or Android version the shell
still supports, to platform APIs that a newer version deprecates. Both shells exempted CA1422 from
warnings-as-errors (`WarningsNotAsErrors` in their csproj), on purpose: each replacement is a migration of
behaviour that no test project can exercise, so migrating blind was worse than the warning. The exemption
was a category, not a list, and the comment beside it named three APIs while a fourth (`AndroidBiometrics`)
had come in under it unseen.

## What Changes

- **iOS follows the system through the call iOS 17 made current.** From iOS 17 a screen registers for the
  two traits it follows, the light or dark mode and the contrast. On 15 and 16 the callback 17 deprecated
  still answers, and from 17 it returns before its base, so the two never both answer.
- **Android asks for a location the way the framework spells out the deprecated calls.** Since KitKat a
  request through a `Criteria` is answered by the framework's fused provider, at high accuracy for "fine".
  From Android 12 the shell writes that request itself: the fused provider, a `LocationRequest` at high
  accuracy, one update for a reading and a 10 s / 10 m cadence for following. Below 12 the `Criteria`
  calls are still current and stay. A single update is deprecated from Android 11, so below 12 a reading
  asks for updates. A reading removes its request when its wait ends, whatever ended it.
- **Android's accessibility nodes and events use the constructors that arrived in API 30.** The `Obtain`
  factories API 33 deprecated stay below 30. From Android 16 a check carries `CheckedState`, a mixed check
  as `Partial`, and a mixed check stays uncheckable, because TalkBack 16 reads a checkable partial node as
  "not checked".
- **Android's biometric availability names the class it always meant.** From Android 11 it asks
  `CanAuthenticate(BIOMETRIC_WEAK)`, which is what the call without arguments answered. The prompt's allowed
  authenticators are written as the bound enum, not as two numbers.
- **The exemption goes.** CA1422 fails the build of either shell again, so a call deprecated on a version the
  shell supports never builds.

A developer writes nothing, and nothing an app sees changes: the A/B on the emulator and the simulator
answered the same on both sides, except that the Android 16 node of a mixed check now carries the partial
state, which TalkBack reads as it read before.

## Impact

- Parts: the iOS shell (`PhotonViewController`) and the Android shell (`AndroidLocation`,
  `AndroidBiometrics`, `PhotonAccessibility`), and both shells' csproj.
- Public surface: none moves. Developer surface: none moves.
- Principles: 9, the structural fix and an instrument that fails over one that warns (the exemption goes,
  rather than a list of tolerated warnings); 14, a gap in the SDK is fixed in the SDK.
