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

The developer declares the capability and WHY, once, on the builder — the same place and the same
shape as everything else about the app:

```csharp
builder.Capabilities
    .UseCamera("Reads the code on your card.")
    .UsePhotoLibrary("Pick the picture for your profile.");
```

A permission has to be in a manifest the OS reads at INSTALL time, long before any of this runs —
so the generator READS these calls at compile time and writes the platform files from them. The
attribute still exists as the transport between the two, but it is generated: nobody types it.

The price is honest and stated where it is paid: the reason must be a constant, and an interpolated
one is a build error (EQ3003) rather than a permission that quietly goes missing and is discovered
by a user, on a device, when the camera does not open.

From that one declaration the SDK writes:

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

  The declaration became FLUENT, which is what an app already does with everything else it
  configures. The generator reads `builder.Capabilities.UseX("…")` out of the app's own source and
  emits both the transport attribute and Android's `UsesPermission`; a build step reads the former
  off the compiled assembly and writes the Apple keys. Confirmed on WalletMobile: two fluent lines
  produced `NSPhotoLibraryUsageDescription` + `NSFaceIDUsageDescription` in the plist and
  `READ_MEDIA_IMAGES` + `USE_BIOMETRIC` in the Android manifest.

  **The same capability on all four.** iOS through PHPicker, Android through the Photo Picker
  (falling back to ACTION_OPEN_DOCUMENT below 13), the browser through a file input. What the three
  modern pickers have in common is the reason none of them asks for a permission: they run OUTSIDE
  the app, hand over exactly what was tapped, and the app never sees the library. Asking for
  READ_MEDIA_IMAGES to use them would put a prompt in front of the user for access the app does not
  want — which is how apps teach users to say no.

  So `GetPermissionAsync` answers Granted on all four. That is not a hole in the model; it is the
  honest answer for a picker whose contract is "you get what was chosen". D2 (biometrics) and D6
  (location) are where NotDetermined and Denied start to mean something.

  Android needed one piece of plumbing: activity results arrive through a callback, which is a
  shape no capability can hand an app. `PhotonActivity.PickAsync` bridges it once, so every
  capability that starts an intent gets to be an ordinary awaitable method.

  **Web parity closed.** A transpiled constructor now resolves its dependencies itself, which is
  what ActivatorUtilities does natively: a parameter whose type is an INTERFACE leaves the
  signature and is resolved from the container before the C# body runs, keyed by the interface's
  name — the one thing that survives the crossing, since a C# type does not exist at run time in a
  browser.

  The rule needed one narrowing, and the committed transpilation found it within the hour: the
  runtime's own interfaces are not dependencies. `IReadOnlyList<AccordionItem>` is how a component
  receives its items, and an Accordion resolving its rows from a container is nonsense. Anything in
  a `System.*` namespace is data.
