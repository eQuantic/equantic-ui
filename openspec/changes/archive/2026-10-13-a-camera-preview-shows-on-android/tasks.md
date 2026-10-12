# Tasks

## 1. The engine

- [x] 1.1 `Yuv420.ToRgba` and `Yuv420.Turned`: YUV 4:2:0 planes with their strides to upright RGBA. Verify: `Yuv420Tests` (14 cases)
- [x] 1.2 A camera preview with a session asks for the next frame before its first frame arrives. Verify: `CameraPreviewTests.ASessionWithNoFrameYet_KeepsTheHostAsking`, which fails with main's emit step

## 2. The Android shell

- [x] 2.1 `AndroidCamera` and `AndroidCameraSession` through Camera2, registered in `AndroidCapabilities`; `PhotonActivity` tells the session when it pauses and resumes. Verify: on the emulator below

## 3. The device

- [x] 3.1 On the Android emulator (API 36): the prompt appears on the first start; refused, `StartPreviewAsync` answers `null` and `Permission` reads `Denied`; granted, the feed shows upright at 480 by 640, clipped by its corner radius with text drawn over it
- [x] 3.2 `Capture()` answers `image/jpeg`, 480 by 640, starting `ffd8`
- [x] 3.3 Home and back, and another app taking the camera: the session gives the camera up and takes it back, and frames continue
- [x] 3.4 The device turned to each landscape and back: the frame becomes 640 by 480 and 480 by 640 again, and the picture's top follows the device's top edge

## 4. Documentation and the suites

- [x] 4.1 The wiki's Capabilities page says which shell answers which contract, in English and Portuguese, on the wiki branch named like this pull request. Verify: `WikiShellRealizationsTests`
- [x] 4.2 `docs/platform-catalog/android.md`'s camera rows, and one `docs/LEDGER.md` line citing #775
- [x] 4.3 The Native.Engine and Web suites, each alone and read by its exit code
