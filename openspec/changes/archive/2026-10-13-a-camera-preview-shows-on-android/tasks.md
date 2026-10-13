# Tasks

## 1. The engine

- [x] 1.1 `Yuv420.ToRgba` and `Yuv420.Turned`: YUV 4:2:0 planes with their strides to upright RGBA. Verify: `Yuv420Tests` (14 cases)
- [x] 1.2 A camera preview with a session asks for the next frame before its first frame arrives. Verify: `CameraPreviewTests.ASessionWithNoFrameYet_KeepsTheHostAsking`, which fails with main's emit step
- [x] 1.3 `FramePair`: a source writes the array that was not handed over and publishes it whole; the emit step keeps a texture per array. Verify: `FramePairTests` (6 cases), and `CameraPreviewTests.ASessionThatAlternatesBetweenTwoArrays_CostsTwoTexturesAndNotOneAFrame`, which fails with the texture kept per session

## 2. The Android shell

- [x] 2.1 `AndroidCamera` and `AndroidCameraSession` through Camera2, registered in `AndroidCapabilities`; `PhotonActivity` tells the session when it starts, stops and resumes. Verify: on the emulator below
- [x] 2.2 `AndroidPermissions`: `Denied` when the last answer was a refusal and Android asks for no rationale, recorded outside the backup. Verify: on the emulator below

## 3. The device

- [x] 3.1 On the Android emulator (API 36), a fresh install: the prompt appears on the first start; refused, `StartPreviewAsync` answers `null` and `Permission` reads `NotDetermined`; the next start prompts again; refused again, `Permission` reads `Denied` and a third start shows no prompt
- [x] 3.2 A fresh install, granted: the feed shows upright at 480 by 640, clipped by its corner radius with text drawn over it, and `Capture()` answers `image/jpeg`, 480 by 640, starting `ffd8`
- [x] 3.3 The camera service's list of active clients: empty after each of five stops and after Home, one entry after each start and after the return, the process the same throughout
- [x] 3.4 The device turned to each landscape and back, beside the system's camera app in the same posture: the frame is 640 by 480 and 480 by 640 again, the picture stands as the reference shows it, and the session takes the camera back each time the reference lets it go
- [x] 3.5 A minute of preview with the two arrays: fewer than a hundred descriptor sets allocated, read from a temporary counter in the Vulkan device
- [x] 3.6 Home pressed between the device opening and its capture session being configured (the bench build holds the camera's thread there for 2.5 s), on a first start and on a reopen, with one script for both sides: as first written the start stays at "starting" and the reopen never lists the camera again; here the start answers `null` and the reopen lists it
- [x] 3.7 A permission refused twice, granted with `pm grant`, read as granted, then revoked with its flags cleared, with one script for both sides: as first written `Permission` reads `Denied` while Android prompts; here it reads `NotDetermined`
- [x] 3.8 The first frame's data space on the emulator: `0x8c20000`, JFIF, which is BT.601 full range

## 4. Documentation and the suites

- [x] 4.1 The wiki's Capabilities page says which shell answers which contract, in English and Portuguese, on the wiki branch named like this pull request. Verify: `WikiShellRealizationsTests`
- [x] 4.2 `docs/platform-catalog/android.md`'s camera rows, and one `docs/LEDGER.md` line citing #775
- [x] 4.3 The Native.Engine and Web suites, each alone and read by its exit code
