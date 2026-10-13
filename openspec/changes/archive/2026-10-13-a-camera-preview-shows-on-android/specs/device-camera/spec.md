## ADDED Requirements

### Requirement: A phone shell answers the camera contract

The Android shell SHALL register a realization of `ICamera`, as the iOS shell does. The wiki's
Capabilities page SHALL say, per contract and in both languages, what each native shell registers:
a realization, an object that always reports itself unavailable, or nothing.

#### Scenario: The table and the registrations

- **WHEN** the table on the wiki's Capabilities page is compared with every shell's `Devices/*Capabilities.cs`
- **THEN** every cell says what that shell registers for that contract, in English and in Portuguese

#### Scenario: A shell that stops registering the camera

- **WHEN** the Android shell registers no `ICamera`
- **THEN** the comparison fails, naming the contract and the shell

### Requirement: The permission is asked where the preview starts

`StartPreviewAsync` SHALL show the system prompt whenever Android still shows one, and SHALL answer
`null` when the permission is not granted. `Permission` SHALL read `Granted` after a grant, `Denied`
when the last answer was a refusal and Android will not show the prompt again, and `NotDetermined`
otherwise, so that `Denied` is never read while asking would still prompt.

#### Scenario: Refused once

- **WHEN** an app on the Android emulator starts a preview for the first time and the prompt is refused
- **THEN** `StartPreviewAsync` answers `null`, `Permission` reads `NotDetermined`, and starting again
  shows the prompt again

#### Scenario: Refused for good

- **WHEN** the second prompt is refused too
- **THEN** `StartPreviewAsync` answers `null`, `Permission` reads `Denied`, and starting again shows
  no prompt

#### Scenario: Granted

- **WHEN** the prompt is granted
- **THEN** `StartPreviewAsync` answers a session whose frames arrive, and `Permission` reads `Granted`

### Requirement: A camera frame reaches the engine as upright pixels

`Yuv420.ToRgba` SHALL convert a YUV 4:2:0 frame, given its planes with their row and pixel strides,
into RGBA, turned by the quarter turns asked for, writing every pixel of the target once. A session
SHALL turn each frame so that it is upright for the display's rotation at that frame.

#### Scenario: Grey and the extremes

- **WHEN** a frame has neutral chroma
- **THEN** each pixel is the grey of its luma, black at 0 and white at 255

#### Scenario: Strides

- **WHEN** the same picture is given with padded rows and with interleaved chroma
- **THEN** the pixels are the same

#### Scenario: A quarter turn

- **WHEN** a 4 by 2 frame is turned by one quarter
- **THEN** the result is 2 by 4, and the frame's first pixel is in the top right corner

#### Scenario: The device turned

- **WHEN** a preview runs on the Android emulator and the device is turned to each landscape and back
- **THEN** the frame is 640 by 480 in landscape and 480 by 640 in portrait, and in each posture the
  picture stands the way the system's own camera app shows it

### Requirement: A preview is an ordinary node from its first render

A `CameraPreview` with a session SHALL keep the host rendering before the session's first frame
arrives, and SHALL be drawn as a texture the display list clips and draws over.

#### Scenario: A session with no frame yet

- **WHEN** a camera preview is emitted with a session that has produced no frame
- **THEN** the frame is marked as one that needs another

#### Scenario: Under a corner radius

- **WHEN** a preview with a corner radius has text positioned over it on the Android emulator
- **THEN** the feed is clipped by the corners and the text is drawn over it

### Requirement: A still is an encoded image

`ICameraSession.Capture` SHALL answer the frame on screen as an encoded image with the upright frame's
dimensions, and `null` before the first frame.

#### Scenario: A still on Android

- **WHEN** `Capture()` is called on a running session on the Android emulator
- **THEN** it answers `image/jpeg` at 480 by 640, and the bytes start `ffd8`

### Requirement: The camera follows the app's place on screen

A session SHALL give the camera up when the app leaves the screen and SHALL take it back when the
app returns, without the page starting a new session. A session that is disposed SHALL give the
camera up for good, and a start that is cancelled SHALL leave no camera open.

#### Scenario: Home and back

- **WHEN** a preview runs on the Android emulator, the app is sent home and then brought back
- **THEN** the camera service lists no client for the app while it is away, and lists it again when
  it is back, in the same process

#### Scenario: Another app takes the camera

- **WHEN** another camera app is opened over a running preview and then closed
- **THEN** the same session's frames continue

#### Scenario: Stopped and started five times

- **WHEN** a page disposes its session and starts a new one, five times over
- **THEN** the camera service lists no client after each stop and one after each start
