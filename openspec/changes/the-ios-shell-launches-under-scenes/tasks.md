# Tasks

## 1. The iOS shell

- [x] 1.1 `PhotonApp.PhotonSceneDelegate` makes the window on its `UIWindowScene`; `PhotonAppDelegate` owns none and answers an unnamed configuration with `typeof(PhotonSceneDelegate)`, for the app's own role only, so a connected display never takes the screen off the phone. Verify: the Wallet sample, built in Release, presents its 60 self-test frames on the iOS 26.5 simulator, and the log carries no scene configuration error, which a named configuration produced
- [x] 1.2 The screen is made once for the process; the clock pauses in the background, and in a scene that connects there, and resumes with a fresh frame. Verify: on the simulator, the Wallet with Income selected goes Home and back in the same process, still shows Income, and selects Bills on the next touch
- [x] 1.3 The public surface: `PhotonAppDelegate.Window` removed, `GetConfiguration` and `PhotonSceneDelegate` added in `PublicAPI.Unshipped.txt`

## 2. The SDK

- [x] 2.1 `PhotonApp.plist` declares `UIApplicationSceneManifest` with one scene and names no delegate class. Verify: `PhotonAppPlistTests`, failing against main's plist, and the built Wallet's Info.plist read with `plutil`

## 3. CI

- [x] 3.1 The `samples` job builds the Wallet in Release for the simulator, reads the scene manifest from its Info.plist, launches it on the newest iOS runtime the runner has with `Photon__MaxFrames`, and fails unless the unified log carries its self-test line, with nothing contained, and no scene configuration error. Verify: green here on a fresh iOS 26.5 simulator; a copy whose app never finishes its frames, and one whose app is terminated after launch, both fail, the second without waiting out the poll

## 4. iOS 27

- [ ] 4.1 On Xcode 27 with an iOS 27 simulator runtime: `main`'s Wallet does not launch and this branch's presents its frames, the same script on both sides

## 5. Documentation and the suites

- [x] 5.1 The wiki's Photon page (EN + pt-BR), in one commit on the wiki branch named like this one
- [x] 5.2 `docs/FLUTTER-PARITY.md`'s lifecycle section, `docs/platform-catalog/ios.md`'s scene row, and one `docs/LEDGER.md` line citing #796
- [x] 5.3 The Native.Engine suite, alone and read by its exit code: 2,104 passed, 19 skipped, exit 0. The parity row's probe fails against main's shell, and against an app delegate given a window after its configuration method
