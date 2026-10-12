# Proposal

Closes #771, a Bug, and #772, a User Story, both under #196 (Platform and lifecycle). Both came out of
the question whether an app built with the SDK can ship to the Play Store today: each was one of its
blockers. Also closes #794, a Bug under #196 that proving #771 found: every rotation crashed the app.

## Why

Measured on the Android emulator (API 36, Android 16, Vulkan through the host GPU) at 1850922:

- Selecting the Wallet sample's Cards tab, pressing Home and returning showed the Home tab. The
  activity and the process were the same ones; Android had taken the `SurfaceView`'s surface away,
  and `PhotonActivity.SurfaceCreated` built a new `PhotonHost` from the app's root when it came back
  (#771). Any app came back on its first screen after the person looked away.
- Back with a dialog on screen closed the activity: the dialog stayed open under it, and the app
  reopened on its first screen. `PhotonActivity` registered no back callback and read no back key, so
  Android's own back ran (#772). Nothing closed a sheet, a drawer or a menu, and a `ListDetail` on a
  phone never went from its detail back to its list.
- Turning the phone crashed the app about 100 ms later, on `main` as on this change's first commits:
  `vkQueuePresentKHR` answered `VK_SUBOPTIMAL_KHR`, which `VulkanSwapchain` tested for under a value
  Vulkan does not define (#794). With the crash out of the way, the window was drawn from a buffer one
  rotation behind: a landscape window showed a portrait buffer squeezed into its top half.

## What Changes

- **The Android activity keeps its host for as long as it lives.** Losing the surface releases only
  what draws (the swapchain, the window, the backend); the surface handed back draws the same tree, so
  the app returns on the screen it left, with its state. A dark-mode or density change while the
  activity runs reaches the host, which before only happened because the host was rebuilt. The
  generated launcher activity also handles a resize into split screen or onto a foldable's other
  display (`SmallestScreenSize`) and a hardware keyboard attached (`Keyboard`, `KeyboardHidden`),
  which rebuilt the activity before.
- **Back closes what is open.** `PhotonHost.Back()` closes the layer on top through the Escape binding
  it already declares (a dismissible `Dialog`, a `BottomSheet`, a `Drawer`, an `Anchored` menu, a
  picker). A field being edited does not take it, nor does a focus ring alone, as in Flutter.
  `PhotonHost.CanGoBack` says up front whether back has anywhere to go.
- **The Android activity answers back.** From Android 13 it registers an `OnBackInvokedCallback` only
  while `CanGoBack` is true, so Android keeps its predictive "leave the app" animation for the rest;
  before 13, and wherever the platform still sends back as a key, `DispatchKeyEvent` takes
  `KEYCODE_BACK` on the same condition.
- **The generated launcher declares predictive back** (`EnableOnBackInvokedCallback`), so Android 14
  and 15 ask the shell's callback too; Android 16 assumes it for an app that targets it.
- **A rotation keeps the app, drawn upright at the window's size.** The swapchain reads
  `VK_SUBOPTIMAL_KHR` as a frame shown and `VK_ERROR_OUT_OF_DATE_KHR` as a rebuild; it declares its
  images upright (`preTransform` IDENTITY), so the compositor turns them to the panel; and it is made
  at the size the window reports, inside the range the surface accepts, rather than at the surface's
  last extent.
- **`ListDetail` goes back on a phone.** Its compact detail binds Escape to `OnBack`, in the compact
  arm only, so back leaves the detail for the list and a wide window, where both panes are on screen,
  binds nothing. On the web, Escape on a narrow window now leaves the detail too.

For a developer: an app keeps its screen when the person switches away and back, and the system back
closes the dialog, sheet, drawer, menu or detail on screen before it leaves the app. Nothing to write:
a layer that closes on Escape closes on back. A screen with a back of its own declares it the same way,
`Shortcut(screen, KeyChord.Escape, GoBack)`.

## Capabilities

### New Capabilities

- `system-back`: what the platform's back closes, and when it is the system's.
- `native-app-lifecycle`: what an app keeps while it is away from the screen.

### Modified Capabilities

None.

## Impact

- Photon: `PhotonHost.Back()` and `PhotonHost.CanGoBack` (public surface, `PublicAPI.Unshipped.txt`).
- The Android shell: `PhotonActivity` (surface lifecycle, configuration changes, back).
- The native generator: the launcher activity's `ConfigurationChanges` and `EnableOnBackInvokedCallback`.
- The Vulkan backend: `VulkanSwapchain`'s present answers, transform and extent; the Windows shell,
  which presents through it too, sees the same extent as before, since a desktop surface fixes it.
- The component library: `ListDetail`, and its transpiled twin in the runtime.
- Not reached: eqc, the web realizer, the iOS and macOS shells, the SDKs and the templates. The
  developer surface does not move.
- Out of scope, and unchanged: the process being killed in the background, which restoring state
  across process death belongs to (#196); back from the first screen with nothing open, which is the
  system's and on Android 16 finishes the activity, so the next launch starts fresh; and a screen's
  state across an adaptive breakpoint, which a phone crosses when it turns (#797).
