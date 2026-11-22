# Tasks

## 1. The sites

- [x] 1.1 iOS: register for the mode and contrast traits from iOS 17, and return early from the deprecated callback there
- [x] 1.2 Android location: the fused provider and a `LocationRequest` from Android 12, the `Criteria` calls below, a reading's listener ending its updates at the first
- [x] 1.3 Android accessibility: the API 30 constructors for nodes, range info and events, `CheckedState` from Android 16 with a mixed check uncheckable
- [x] 1.4 Android biometrics: `CanAuthenticate(BIOMETRIC_WEAK)` from Android 11, the allowed authenticators as the bound enum
- [x] 1.5 Remove the CA1422 exemption from both shells' csproj
- [x] 1.6 Check: main's code without the exemption fails the build on exactly the eleven CI sites, and this change builds both shells with no warning

## 2. Proof on the emulator and the simulator

- [x] 2.1 Android 16 emulator, main against this change: one reading and following registered and delivered the same, and stopping removes the registration
- [x] 2.2 Android 16 emulator: the branch below Android 12, forced in a bench build, reads a location and its listener ends the updates
- [x] 2.3 Android 16 emulator: the accessibility tree and TalkBack's words for every node match main, walked with TalkBack's next-item key
- [x] 2.4 Android 16 emulator: the biometric service receives the same availability query from both sides, and the prompt accepts the device PIN
- [x] 2.5 iOS 27 simulator: the mode and the contrast re-render as on main, and a build without the registration follows neither

## 3. Documentation

- [x] 3.1 One ledger entry under `docs/ledger/`
