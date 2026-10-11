# Tasks

## 1. Back in the host

- [x] 1.1 `PhotonHost.Back()` and `PhotonHost.CanGoBack`: the topmost Escape binding runs, or with none the edit ends; a focus ring alone takes nothing. Verify: `SystemBackTests` (12 cases)
- [x] 1.2 `ListDetail`'s compact detail binds Escape to `OnBack`, in the compact arm only. Verify: `SystemBackTests.OnAPhone_BackLeavesTheDetailForTheList`, failing against main, and the web's `ListDetailTests.OnlyTheCompactDetailBindsEscapeToBack`
- [x] 1.3 The transpiled library's pin regenerated and read: `ListDetail` binds the Shortcut

## 2. The Android shell

- [x] 2.1 `PhotonActivity` keeps its host across the surface, and follows dark mode and density in `OnConfigurationChanged`; the generated launcher activity handles a resize and a hardware keyboard. Verify: on the emulator, the Wallet keeps its Cards tab through Home, the screen turning off, another app, a rotation and a dark-mode change while away
- [x] 2.2 `PhotonActivity` registers its back callback only while the host can go back, and takes `KEYCODE_BACK` on the same condition. Verify: on the emulator, the template's list-detail and drawer apps close their detail and drawer on back and leave on the next, through the callback and, opted out of predictive back, through the key
- [x] 2.3 The generated launcher activity declares `EnableOnBackInvokedCallback`. Verify: `PhotonProgramGeneratorTests.TheAndroidLauncherFollowsItsOwnChangesAndDeclaresPredictiveBack`, failing without it, and the Wallet APK's manifest
- [ ] 2.4 A density change lays out at the new size in dp, a frame lost to a rebuilt swapchain is drawn again, the back key's UP follows the DOWN that decided it, and the launcher handles a `Navigation` change. Verify: `PhotonProgramGeneratorTests`, and on the emulator `wm density` with the Wallet in front, then a rotation and back as before

## 3. The Vulkan swapchain (#794)

- [x] 3.1 `SwapchainStatus`: `VK_SUBOPTIMAL_KHR` is a frame shown, `VK_ERROR_OUT_OF_DATE_KHR` a rebuild, anything else an error. Verify: `SwapchainStatusTests`, and on the emulator the Wallet and the drawer app turning in front without a crash, which crashed on `main`
- [x] 3.2 `SwapchainShape`: `preTransform` IDENTITY, and the extent the window reports inside the surface's range. Verify: `SwapchainShapeTests`, and on the emulator the buffer matching the window after each turn, drawn upright, including a launch in landscape

## 4. Documentation and the suites

- [ ] 4.1 The wiki's Photon and Components pages (EN + pt-BR), in one commit on the wiki branch named like this one
- [x] 4.2 `docs/FLUTTER-PARITY.md`'s back row and one `docs/LEDGER.md` line citing #771, #772 and #794
- [ ] 4.3 The suites, each alone and read by its exit code: Native.Engine, Web, Compiler, Server and the runtime's `TestRuntime`
