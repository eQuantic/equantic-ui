# Proposal

Closes #775, a Feature under #774 (Track D: a phone's capabilities in C#).

## Why

`AndroidCapabilities` registered no `ICamera`, so on Android the service resolved to `null` and a
`CameraPreview` drew its unavailable state, while the same C# showed the live feed on iOS and macOS.
`UseCamera(reason)` still wrote `android.permission.CAMERA` into the manifest, so an app asked for a
permission the shell never used.

## What Changes

- **`ICamera` on Android**, through Camera2 over the Mono.Android binding the shell already uses.
  CameraX is a Jetpack library on top of the same API, and a preview and a still need nothing it adds,
  so the SDK ships no connector and an app gains no dependency.
- **The permission is inside `StartPreviewAsync`**, as on iOS: it shows the system prompt whenever
  Android still shows one, and answers `null` when the permission is not granted. `Permission`
  reports three answers, though Android gives two: `Denied` is a refusal Android will not ask about
  again, which on Android 11 and later is the second one.
- **Frames reach the engine as pixels.** An `ImageReader` hands each YUV frame over, and the engine's
  new `Yuv420.ToRgba` converts it and turns it upright for the way the device is held, into one
  reused buffer the renderer uploads when its version moves. The preview stays an ordinary node that
  clips, takes a corner radius and sits under what is drawn over it.
- **`Capture()`** returns the frame on screen as an encoded still (`image/jpeg`).
- **The camera follows the app's place on screen.** It closes when the activity leaves the screen
  and opens again when it returns, in the same session. A paused activity that is still in view
  (beside another window, under a prompt) keeps its preview.
- **A preview with a session keeps the host rendering from its first render.** The emit step asked
  for the next frame only once a frame had arrived, so a preview started on a still screen stayed
  blank until something else redrew. Found on the emulator, and it affected every shell.
- **The wiki says which shell answers which contract**, in both languages, and a guard compares that
  table with the shells' registrations.

For a developer: nothing new to write. `UseCamera(reason)` and a `CameraPreview` now show the feed on
Android as they do on iOS.

## Capabilities

### New Capabilities

- `device-camera`: what the camera contract answers on a phone, how a frame reaches the engine, and
  where a reader learns which shell realizes which contract.

### Modified Capabilities

None.

## Impact

- **The Android shell** (`eQuantic.UI.Native.Shell.Android`): `AndroidCamera`, `AndroidCameraSession`,
  `AndroidPermissions` (the three answers, for any permission), the registration in
  `AndroidCapabilities`, and `PhotonActivity`, which tells the session when it starts, stops and
  resumes, and now says whether a permission request was granted, refused or left unanswered.
- **The engine** (`eQuantic.UI.Native.Engine`): `Yuv420`.
- **The native components** (`eQuantic.UI.Native.Components`): the camera preview's emit step.
- **Public surface moves** by additions only: `Yuv420` with `ToRgba` and `Turned`, and
  `PhotonActivity`'s `OnStart`, `OnStop` and `OnResume` overrides. The developer surface does not
  move. Nothing breaks.
