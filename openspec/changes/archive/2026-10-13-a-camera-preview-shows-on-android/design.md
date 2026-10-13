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
frame costs one pass. BT.601 full range is what an Android `YUV_420_888` reader is given (the public
format maps to the JFIF data space), and the first frame on the emulator reported exactly that:
`0x8c20000`. A stream in another range or matrix is one a session would have to ask for by name.

**Two arrays, turn about.** The camera converts a frame on its own thread and the renderer uploads
the published one on another. A lock between them would hold the render thread for as long as a
conversion takes, so there is none, and with one array an upload that landed in the middle of a
conversion drew the top of one frame over the bottom of another (found by Copilot's review). The
session writes the array the renderer was not handed and publishes it when it is whole, so what a
renderer reads stays untouched for a whole frame of the camera. `FramePair` is that handoff, in the
engine because it is no platform's. The realizer then keeps a texture per ARRAY: per session, as it
was, a session that alternates would change texture on every frame it delivers. The Apple session
still writes one array, which is #870. What this does not close is the frame's size, which a
realizer reads apart from its bytes: a phone turned between the two reads gives one render the old
scanlines in the new shape. That is the contract's four separate reads, and it is #871.

**Upright is the session's job.** A phone's sensor is mounted sideways. The frame turns by the
sensor's orientation less the display's rotation (their sum for a front camera), read for every
frame, so a phone turned while the preview runs keeps the picture the right way up.

**640 by 480.** The preview's resolution on the Apple shells, and the conversion costs by the pixel.

**Three answers from two.** Android says "granted" or "not". The contract's `Denied` means "asking
again does nothing", and on Android a first refusal still leaves one more prompt, so a remembered
"asked" is not enough: measured on the emulator, the prompt appeared again after one refusal. A
permission is refused for good when two facts hold together: the last answer the prompt gave was a
refusal, which the shell records, and Android no longer asks for a rationale, which is how it says it
will not prompt. Only an answer is recorded (a prompt interrupted refused nothing), a grant clears
the record (so a grant "only this time" that expired reads as askable), and the record lives outside
the app's backup, where a restore onto a new phone cannot bring a refusal nobody gave. A grant the
shell merely SEES clears it too: one made in the system's settings never passes through the prompt,
and a record left behind it read `Denied` again once that grant was reset, while Android prompted
(found by Copilot's review, and measured). This is `AndroidPermissions`, written for any permission:
location still answers two ways and can take it.

**Stopped, not paused.** Android takes the camera from an app nobody sees anyway, and closing it
first is what keeps the session able to open it again. But a paused activity may be in full view,
beside another window or under a prompt, and a preview that froze there would be letting the camera
go while the person looks at it. The session releases on stop and opens again on start. It also asks
again on resume, which costs nothing when the camera is open and is how a camera lost to the window
that had the focus comes back.

**An open is an attempt, and callbacks carry theirs.** The activity may leave the screen at any
point of an open: before the device arrives, or between the device and its capture session. The
callbacks first spoke for "the session", and a release in the middle left the open pending with
nobody to finish it: measured on the emulator with the camera's thread held between the two steps,
a first start never answered and a reopen never came back (found by Copilot's review). Each open is
now an `Opening` the platform's callbacks are constructed with. Whoever gives the camera up settles
the attempt it abandons, and what that attempt still has to say arrives for an attempt that is no
longer the session's own, where it closes what it was handed and changes nothing. A start that is
cancelled disposes the session it was opening, and a failed open ends the same way whether the
failure came back or was thrown.

**A frame is read under the session's gate.** Disposing closes the image reader, which closes the
image a frame in the middle of its conversion is still reading, on a thread where nothing would
catch that. The frame and the close take the same gate. Each Java object a frame touches is disposed
as it is read: the path allocates nothing managed, so nothing else would release their references.

**One camera thread for the process.** A thread per session would have to outlive the session by as
long as the camera goes on reporting about a device being closed. One that quit sooner turned each
of those reports into a message to a dead thread, which Android logs with a stack trace on every
stop. A looper with nothing to do costs nothing, so there is one and it is never told to quit.

**The table is compared with the registrations.** "Registered" is not "realized": the Windows shell
registers objects that always report themselves unavailable. A cell therefore has three answers, and
the guard reads the third from the implementation, whose `IsAvailable` is the literal `false`.

## What is proved where

`Yuv420Tests` (14 cases) holds the conversion: grey and the primaries, padded rows and interleaved
chroma, the corner each quarter turn moves, every pixel written once. `FramePairTests` holds the
handoff: the array handed over is not written until another frame has been published after it, and a
source that runs uses two arrays. `CameraPreviewTests` holds the emit step: the case with no frame
yet fails with main's, and the case of two alternating arrays fails with the texture kept per
session. `WikiShellRealizationsTests` holds the table, and fails with the Android registration
removed and against a wiki without the table.

The shell itself is Camera2 on a device, so it is proved on the Android emulator (API 36). The
prompt: refused once, `NotDetermined` and the prompt again; refused twice, `Denied` and no prompt;
granted on a fresh install, the feed upright at 480 by 640 under a corner radius with text over it.
A still that is a JPEG of the same size. The camera service's own list of clients, read after each
step: empty after each of five stops and after the app goes home, one entry after each start and
after it comes back, in one process. The device turned to each landscape and back, with the system's
own camera app opened in the same posture as the reference: the two show the picture the same way up
each time, and the session takes the camera back from it. A minute of preview allocates fewer than
a hundred descriptor sets, which is two textures for the feed and not one per frame.
