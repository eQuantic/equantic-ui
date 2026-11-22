# Design

## How Flutter solves it

Flutter's embedders keep the platform's versions out of the framework: the iOS and Android embedders
branch on the OS version where a platform call was deprecated, and the framework above them never sees the
split. The shells already do the same (`OperatingSystem.Is…VersionAtLeast` guards in
`PhotonAccessibility` and `AndroidBiometrics`), and this change finishes it for the calls left.

## The rule each site follows

Each site calls the API that is current from its version upward, and the deprecated one only below the
version that deprecated it, where it is still current. The platform analyzer reads those guards, so with the
exemption gone a call left on the wrong side fails the build.

## Location: the framework's own expansion

The Android 16 sources say what the deprecated `Criteria` calls do: `requestSingleUpdate(Criteria, …)` is
`requestLocationUpdates(FUSED_PROVIDER, new LocationRequest.Builder(0).setQuality(criteria).setMaxUpdates(1)
.setDurationMillis(30 s).build(), …)`, and `requestLocationUpdates(long, float, Criteria, …)` builds the same
request with the interval and the distance. A fine `Criteria` sets `QUALITY_HIGH_ACCURACY`. From Android 12,
where `FUSED_PROVIDER` is public, the shell writes those requests itself. It leaves out the 30 s duration,
because the reading removes its request when its wait ends, whatever ended it: a location, the provider
switched off, the 15 s, or the token.

The review of this change found that the provider switched off had been the one path that did not: the
listener answered `null` and the request stayed registered. Measured on the Android 16 emulator by switching
location off during a reading: main's request left after the platform's 30 s duration, the reviewed code's
stayed (no `-registration` 35 s later), and this change's left at once, as the reading answered.

Measured with `dumpsys location` on the Android 16 emulator, main and this change register the same request:
`Request[@0 HIGH_ACCURACY, maxUpdates=1]` for a reading (main's adds the 30 s duration), and
`Request[@+10s0ms HIGH_ACCURACY, minUpdateDistance=10.0]` for following, both on the fused provider.

## Accessibility: what TalkBack reads

Android 16 added `CheckedState`, with a partial state. Marking a mixed check checkable and partial looked like
the current mapping, but TalkBack 16.0 read it as "not checked, Mixed box, Check box", which claims the box is
off. So the node of a mixed check carries the partial state and stays uncheckable, and TalkBack reads
"Mixed box, Check box", as it did before this change.

## iOS: one answer per change

UIKit on iOS 17 and later still calls the deprecated `traitCollectionDidChange`. The override returns there
before reaching its base, so the registration is the only answer. On the iOS 27 simulator a build without the
registration followed neither the mode nor the contrast, which shows the registration is the live path.
The handler is static and reaches the screen through the trait environment UIKit hands it: the controller
keeps its registration, so a handler capturing the controller would keep the controller alive (the secondary
review's finding). The static handler re-rendered the same as main on the iOS 27 simulator.

## What was not run on an older system

No iOS 15 or 16 runtime and no Android image older than 16 is installed here. Below the versions that
deprecated them, the sites call exactly what main called, and main's build ran those calls in the same A/B.
The one new line below a version is the reading's listener ending its own updates below Android 12, and a
bench build that forced that branch on Android 16 ran it.
