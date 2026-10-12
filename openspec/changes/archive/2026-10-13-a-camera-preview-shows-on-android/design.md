# Design

## How Flutter solves it

Flutter has no camera in its framework: the `camera` plugin is a package an app adds, which talks to
CameraX (and before that Camera2) over a method channel and shows the feed through a `Texture`
component backed by a platform surface. The `MethodChannel` row of `docs/FLUTTER-PARITY.md` is the
difference this track is about: here the camera is a typed contract the shell realizes, and an app
adds nothing.

## Decisions

**Camera2, not CameraX.** The issue left the choice to this change. Camera2 is the framework's own
API and .NET for Android already binds it, so the shell needs no new package. CameraX would arrive
as `Xamarin.AndroidX.Camera.*` packages in every app, or as a connector the SDK builds, and what it
adds (lifecycle binding, use cases, device quirks for capture pipelines) is beyond a preview and a
still. If video (#784) needs it, that change brings it for its own reason.

**Frames as pixels, as on the Apple shells.** A platform preview surface composites outside the
engine, above every clip, corner radius and overlay the display list draws. An `ImageReader` in
`YUV_420_888` hands each frame over, and the preview is a texture like any other.

**The conversion lives in the engine.** `Yuv420.ToRgba` takes planes with their row and pixel
strides, which is how every platform hands YUV over, so it is not Android's. It uses BT.601 full
range in 16.16 fixed point, and it turns the frame by quarter turns while it converts, so an upright
frame costs one pass.

**Upright is the session's job.** A phone's sensor is mounted sideways. The frame turns by the
sensor's orientation less the display's rotation (their sum for a front camera), read for every
frame, so a phone turned while the preview runs keeps the picture the right way up.

**640 by 480.** The preview's resolution on the Apple shells, and the conversion costs by the pixel.

**Three answers from two.** Android says "granted" or "not", and cannot tell "never asked" from
"refused". The shell records that it asked, in its own preferences, after the prompt is answered:
while the prompt is up nobody has refused anything.

**The session lets the camera go first.** Android takes the camera from an app in the background
anyway. Closing it on pause is what keeps the session able to open it again on resume, and the last
frame stays in the buffer meanwhile.

**The table is compared with the registrations.** "Registered" is not "realized": the Windows shell
registers objects that always report themselves unavailable. A cell therefore has three answers, and
the guard reads the third from the implementation, whose `IsAvailable` is the literal `false`.

## What is proved where

`Yuv420Tests` (14 cases) holds the conversion: grey and the primaries, padded rows and interleaved
chroma, the corner each quarter turn moves, every pixel written once. `CameraPreviewTests` holds the
emit step, and fails with main's. `WikiShellRealizationsTests` holds the table, and fails with the
Android registration removed and against a wiki without the table.

The shell itself is Camera2 on a device, so it is proved on the Android emulator (API 36): the prompt,
a refusal that reads `Denied` and answers `null`, a grant that shows the feed upright at 480 by 640
under a corner radius with text over it, a still that is a JPEG of the same size, the camera given up
to another app and taken back, and the device turned to both landscapes and back.
