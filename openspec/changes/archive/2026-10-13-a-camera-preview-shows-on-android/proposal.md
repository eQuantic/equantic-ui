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
- **The permission is inside `StartPreviewAsync`**, as on iOS: it shows the system prompt when the
  permission was never asked and answers `null` when it is refused. `Permission` reports three
  answers, though Android gives two: the shell remembers that it asked, after the prompt is answered.
- **Frames reach the engine as pixels.** An `ImageReader` hands each YUV frame over, and the engine's
  new `Yuv420.ToRgba` converts it and turns it upright for the way the device is held, into one
  reused buffer the renderer uploads when its version moves. The preview stays an ordinary node that
  clips, takes a corner radius and sits under what is drawn over it.
- **`Capture()`** returns the frame on screen as an encoded still (`image/jpeg`).
- **The camera follows the app's place on screen.** It closes when the activity pauses and opens
  again when it resumes, in the same session.
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
  the registration in `AndroidCapabilities`, and `PhotonActivity`, which tells the session when it
  pauses and resumes.
- **The engine** (`eQuantic.UI.Native.Engine`): `Yuv420`.
- **The native components** (`eQuantic.UI.Native.Components`): the camera preview's emit step.
- **Public surface moves** by additions only: `Yuv420` with `ToRgba` and `Turned`, and
  `PhotonActivity`'s `OnPause` and `OnResume` overrides. The developer surface does not move. Nothing
  breaks.
