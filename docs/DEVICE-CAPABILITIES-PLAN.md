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

- **2026-08-04 (later)** — **D2 biometrics**, and the first capability that does not work yet.

  The contract went in with a six-way result, for the same reason a permission is not a boolean:
  "did not succeed" hides a wrong finger (try again), a device with nothing enrolled (go to
  Settings), someone who backed out (leave them alone), and no reader at all (never offer this).
  An app that collapses them either nags or gives up in silence.

  Apple's side is shared between the Mac and the iPhone — LocalAuthentication is the same framework
  and only the sensor differs, which is exactly the kind of difference an app should never see.
  Android uses the FRAMEWORK's BiometricPrompt rather than AndroidX: same prompt, no dependency an
  app carries for a capability it may never use. The browser reports itself unavailable, on purpose:
  WebAuthn looks similar from a distance but is a different contract (a credential registered
  against a server), and pretending otherwise would hand apps a method that fails in ways the
  interface cannot describe.

  **It works on macOS**, and the way it was WRONG is the lesson worth keeping.

  The prompt appeared, the user authenticated, the reply came back — and the app died a second
  later, in `_Block_release`, on a background thread. The hand-built block claimed to live on the
  stack. A synchronous API borrows a block and hands it straight back; an asynchronous framework
  OWNS it — copies it on the way in, releases it when done — and releasing a stack block means the
  runtime copying and freeing memory this code allocated and still frees itself.

  The block now declares itself GLOBAL: copy hands back the same pointer, release does nothing, and
  the lifetime stays with the handle that already keeps the delegate alive.

  Two tests had already passed on the broken version, which is the part worth writing down. They
  checked the block against `enumerateObjectsUsingBlock:` — called three times, arguments intact —
  and proved the CALL while never touching the LIFETIME. The bug lived entirely in the half the
  synchronous API does not exercise. `ItSurvivesTheCOPY_AND_RELEASE_AnAsyncFrameworkDoes` does the
  copy and release by hand, and fails on the old version.

  A reply that never comes still times out into `Failed` rather than leaving the promise pending
  for ever — worth keeping regardless of the cause. iOS and Android are written but were run on
  neither.

- **2026-08-04 (later still)** — the macOS bundle was MALFORMED, and finding it was worth the hunt
  even though it did not fix the biometrics.

  Found while hunting the crash above, and worth its own line. Everything the build produced was being copied into `Contents/MacOS`, debug symbols and the
  runtime packs for Windows, Linux, Android and iOS included. `codesign` refuses a bundle with
  unsignable files there, so a single stray `.pdb` left the app unsigned — and the identity it
  reported was `apphost`, the .NET host's, not the app's. An app that cannot say who it is gets
  refused by the capabilities that ask, silently.

  The SDK now copies only what runs, and signs ad hoc under the bundle's own identifier
  (`--deep`, because the managed assemblies beside the apphost each need one of their own).

- **2026-08-04 (interaction)** — the native window had no KEYBOARD, and finding out cost a form
  nobody could fill in. Written up in full in the commit; the two lessons that generalise:

  A golden can bless a bug. `pressed-button.png` and `focus-ring.png` were recorded without the
  press token and without the focus ring — a button that never darkens is still a perfectly good
  button, so every pixel matched. Anything a test claims to prove about STATE deserves an assertion
  next to the image.

  And the identity rule earned another scar: a `Button` is a component, so the `Pressable` inside it
  is rebuilt on every layout. Reference identity fails on the very next frame even with no state
  change at all — not just across a `SetState`, which is how it kept looking almost-right.

- **2026-08-04 (D4)** — **network status**, the first capability whose answer does not hold still.

  The contract is `Current` (answers from the monitor's cache, safe in a Build) plus
  `Subscribe(Action<NetworkState>)` returning an IDisposable — a disposable rather than a .NET
  event, deliberately: components are torn down and rebuilt constantly, and an event subscription
  has no end you can see. The kind (`Wifi`/`Cellular`/`Wired`/`Other`) is what "should I download a
  gigabyte here" reads.

  Apple's realization is NWPathMonitor — the same framework on the Mac and the iPhone — and it is
  the first DIRECT beneficiary of the ObjCBlock lifetime fix: the monitor OWNS its update handler,
  copies it on install and releases it on cancel, exactly the cycle that used to crash after the
  biometric prompt. The dylib path was probed with dlopen before being written down: the C API
  lives in the framework binary, not in the plausible-looking /usr/lib/swift path.

  Proven END TO END in the window, with the app's own log because the remote-automation route
  cannot see through an offline window (requests do not arrive while the network is down —
  learned the honest way): launch → `Online, Wifi`; `networksetup` wifi off → `Offline, None`;
  wifi on → `Online, Wifi`, all flowing through Subscribe → SetState with no refresh anywhere.

  Android reads ConnectivityManager's default-network callback and answers `Current` from the
  ACTIVE network rather than a cached callback (a phone that never changes networks would
  otherwise answer Offline forever); compiled, not run. Web is `navigator.onLine` and the
  online/offline events; the kind is honestly `other` while online rather than a guess, because
  the Network Information API never shipped outside Chromium. `UseNetworkStatus()` takes no reason
  string — nothing anywhere shows one, and a demanded sentence nobody reads teaches filler; it
  maps to ACCESS_NETWORK_STATE on Android and to nothing at all on Apple.

- **2026-08-04 (D5)** — **motion sensors**, and the first capability whose DESKTOP story is the
  absence.

  `MotionReading` carries what apps want rather than what hardware separates: user acceleration
  with gravity already removed (shake detection becomes a threshold instead of a subtraction every
  app writes badly), gravity as its own vector (the input tilt UIs actually read), and rotation
  rate. One unit set across platforms — g and radians per second, CoreMotion's frame — with the
  conversions living where the differences do: Android divides its m/s² by standard gravity, the
  web additionally converts its degrees per second.

  Apple is CMMotionManager through the same shared shell as biometrics and the network, with the
  framework dlopen'd before `objc_getClass` can find the class. The desktop answer comes from
  CoreMotion ITSELF — `isDeviceMotionAvailable` says no on a Mac — not from a platform check
  written here, and the real-framework test pins exactly that. The handler is the global ObjCBlock
  again, at sensor rate this time. The hardware powers down with the last subscriber: a running
  gyroscope is a battery cost an idle page should not carry.

  Web availability is stated honestly: the DeviceMotionEvent constructor exists on desktop
  browsers that will never fire it, so `isAvailable` also wants a coarse touch check, and iOS
  Safari's `requestPermission` gate is honoured where it exists (asked outside a user gesture,
  Safari says no and means it). Android fuses LinearAcceleration + Gravity + Gyroscope listeners
  into the one reading; compiled, not run.

  The Studio's Device section shows the whole point on a desk: "No motion sensors on this desk —
  and that is the design working, not a gap." The same page on a phone streams readings with no
  new line written.

- **2026-08-04 (D6)** — **location**, where PermissionState earns its three values — and where a
  LATENT BUG in the declaration pipeline surfaced and died.

  The contract: `Permission` never prompts (reading a property must not put a dialog on screen),
  `GetCurrentAsync` prompts when the answer is still NotDetermined and resolves null for every
  flavour of "you do not get a position" — Permission says which flavour. Apple is CoreLocation
  with a RUNTIME delegate class (`objc_allocateClassPair`, the content view's own trick), and one
  behaviour cost a round: CoreLocation calls `didChangeAuthorization` IMMEDIATELY when the
  delegate is installed, with the status as it already was — which is not the user's answer, it is
  the absence of one, and resolving the ask on it closed the prompt three seconds after it opened.
  NotDetermined now resolves nothing.

  The bug worth the log line: the capability attribute used to carry the enum's ORDINAL, and the
  day NetworkStatus was inserted mid-enum every declaration after it silently became a different
  one — the Studio asked for Location and its manifest got NSMotionUsageDescription, because the
  writer (the app's generator) and the reader (the build tool) are different assemblies built at
  different times. The attribute now carries the NAME; the ordinal table is deleted. A name cannot
  be renumbered.

  The macOS bundle now receives capability keys at all — the manifest step only ran for iOS, so no
  mac app could ever have shown a permission prompt. `eqicon bundle --capabilities <assembly>`
  reads the same attributes the iOS partial plist is written from, and writes BOTH location
  spellings (the Mac's `NSLocationUsageDescription` and the phones' WhenInUse — an unread key is
  inert, a missing one silently suppresses the prompt).

  Proven END TO END in the window: press → live GPS fix with ±35 m through CoreLocation → runtime
  delegate → SetState. Android is the framework LocationManager (no Play Services dependency) plus
  a RequestPermissionsAsync bridge on PhotonActivity — the permission twin of PickAsync; compiled,
  not run. Web is navigator.geolocation, whose contract this IS; the permission property is a
  lazily-honest cache because the Permissions API answers asynchronously.

- **2026-08-04 (D7)** — **the camera**, and with it the track's last box: LIVE VIDEO in the
  vocabulary, running in the window.

  The node first. `CameraPreview(session, w, h)` is the vocabulary's first surface whose pixels
  the engine does not draw from a display list alone — and it is composited as an ORDINARY
  texture, deliberately. AVCaptureVideoPreviewLayer would have been zero-copy and OUTSIDE the
  engine: above every clip, radius, transform and overlay the display list draws. A preview that
  ignores the page's own geometry is not a node, it is a hole. One BGRA→RGBA swizzle per frame is
  the price, and the M-series does not notice it.

  The engine grew the one primitive this needed: `TextureData.Version`. The session mutates ONE
  byte array in place and bumps the number; the renderer's identity cache re-uploads the same GPU
  slot when it moved. Without this, 30 new instances per second would have met a cache that never
  evicts — a GPU leak with a frame rate (and the audit found the existing smaller one: an Image
  whose source changes already leaked a texture).

  PROVEN LIVE: the Studio's Device section streamed the FaceTime camera into the window — real
  faces, moving, through AVCaptureSession → runtime delegate → per-frame swizzle → texture →
  Metal. Capture hands back a BMP still (54-byte header written by hand; a capture is an input to
  the app's pipeline, not an archival format) that the same page shows via data URI. Stop disposes
  the session and the camera LIGHT goes out — verified by the menu bar's own indicator.

  Web is getUserMedia: the node lowers to a muted autoplaying `<video>` carrying the session id,
  and the stream attaches in the same after-the-pass moment the shortcut set commits. Pixels never
  cross into script; capture is a canvas drawImage → PNG, the browser's own encoder. The C#
  WebRealizer twin pins both shapes (no session = the placeholder div, which is every SSR).

  KNOWN ISSUE, stated rather than hidden: the 640×480 session preset does not take on this Mac —
  canSetSessionPreset says yes, setSessionPreset is called (before AND after addInput; both were
  tried), and frames arrive at 1920×1080 anyway. The preview works; the cost is a bigger swizzle.
  Next lever when it matters: setting the AVCaptureDevice's activeFormat directly.
