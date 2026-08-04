# Device capabilities — plan (Track D)

An app needs the device: the camera, the photo library, the motion sensors, the network's state,
the fingerprint reader. Today a Photon app can draw anything and reach none of it.

The rule the whole track answers to is the product's own: **the developer writes C# and nothing
else.** No Swift, no Kotlin, no JavaScript — and no platform files either. Nobody edits an
`Info.plist` to explain why the app wants the camera, and nobody adds a `<uses-permission>` line to
an `AndroidManifest.xml`.

## The shape

A capability is a **service**, declared as an interface in the vocabulary and taken through a
constructor. That is not a new idea invented here — it is the shape the framework already has: a
`WalletApp` already receives an `IWalletLedger` and an `IConfiguration` that way, and both the
native host and the browser runtime already carry a service provider.

```csharp
public sealed class ScanPage(IPhotoLibrary library, IHaptics haptics) : StatefulComponent
{
    private ImageData? _picked;

    private async Task Pick()
    {
        // Asking IS the permission flow: no separate "request" step to forget.
        if (await library.PickImageAsync() is { } image) SetState(() => _picked = image);
        await haptics.TapAsync();
    }
}
```

Three consequences worth stating:

- **Testable without a device.** An interface has a fake. A page that scans a document can be tested
  by handing it an `IPhotoLibrary` that returns a fixture — no simulator, no camera roll.
- **One page, four targets.** The same constructor is satisfied by the iOS realization, the Android
  one, the macOS one, and a browser one written in TypeScript beside the other runtime twins.
- **Absent is a first-class answer.** A desktop has no gyroscope and a browser has no biometrics
  worth the name. `IMotionSensor.IsAvailable` is part of the contract, not a surprise at run time.

## Permissions

The developer declares the capability and WHY, once, in C#:

```csharp
[assembly: PhotonCapability(DeviceCapability.Camera, "Reads the code on your card.")]
```

An assembly attribute rather than a fluent call because the BUILD needs it: the reason string is
what iOS shows in its permission sheet and what Android's manifest has to carry. From that one
declaration the SDK writes:

- the `NSCameraUsageDescription` key into the app's `Info.plist`;
- the `<uses-permission android:name="android.permission.CAMERA" />` into the manifest (through the
  generator, as an assembly attribute — the same route the launcher icon already takes);
- nothing at all on web, where the browser asks at the moment of use.

Declaring a capability the app never uses is a build WARNING, not a silent extra permission: an
app that asks for the microphone and never records is an app users learn to distrust.

## The capabilities

Ordered by what each one proves, not by how impressive it is.

| # | Capability | Interface | Proves |
|---|---|---|---|
| D1 | Photo library | `IPhotoLibrary` | permission → system UI → binary data → `Image` |
| D2 | Biometrics | `IBiometrics` | a prompt with a yes/no answer, and graceful absence |
| D3 | Haptics | `IHaptics` | fire-and-forget, no permission, no result |
| D4 | Network status | `INetworkStatus` | a VALUE THAT CHANGES — a stream into `SetState` |
| D5 | Motion sensors | `IMotionSensor` | a high-rate stream, and a sensor a desktop lacks |
| D6 | Location | `ILocation` | coarse/fine permission, and a value that changes slowly |
| D7 | Camera | `ICamera` | still capture, then a LIVE PREVIEW — a new node in the |
|    |            |            | vocabulary, since a video surface is not a rect the |
|    |            |            | engine can draw from a display list alone |

D1 goes first because it exercises every part of the pattern end to end with no new engine work.
D7 goes last because a live preview is the only one that needs the renderer to composite something
the engine did not draw, and that decision deserves the pattern to be settled first.

## Status log

- **2026-08-04** — plan written. The shape landed: capabilities as services, discovered per shell
  through `IPhotonCapabilities` the same way the runner already is, and registered LAST so an app's
  own registration (a fake in a test) wins. `IPhotoLibrary` realized on macOS with an open panel,
  and the Studio grew a **Device** section that takes it through its constructor — confirmed in the
  window end to end: system panel → chosen file → bytes → an `Image`, with `1176×877 · 870 KB ·
  image/png` read from the header.

  Two pieces the demo asked for and got: `ImageHeader` (dimensions from PNG/JPEG/GIF headers,
  (0,0) for anything else) and `DataUri` (an image the user picked may never have been a file, so
  the source string carries the bytes — native loaders decode it, browsers already do).

  Next: writing the platform keys from `[assembly: PhotonCapability]` — the step where "nobody
  opens an Info.plist" becomes true — then the same capability on iOS, Android and web.
