# Proposal

Closes #771, a Bug, and #772, a User Story, both under #196 (Platform and lifecycle). Both came out of
the question whether an app built with the SDK can ship to the Play Store today: each was one of its
blockers.

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

## What Changes

- **The Android activity keeps its host for as long as it lives.** Losing the surface releases only
  what draws (the swapchain, the window, the backend); the surface handed back draws the same tree, so
  the app returns on the screen it left, with its state. A dark-mode or density change while the
  activity runs reaches the host, which before only happened because the host was rebuilt. The
  generated launcher activity also handles a resize into split screen or onto a foldable's other
  display (`SmallestScreenSize`) and a hardware keyboard attached (`Keyboard`, `KeyboardHidden`),
  which rebuilt the activity before.
- **Back closes what is open.** `PhotonHost.Back()` runs what Escape runs on a desktop: the edit ends,
  or the layer on top closes through the Escape binding it already declares (a dismissible `Dialog`, a
  `BottomSheet`, a `Drawer`, an `Anchored` menu, a picker). A focus ring alone does not take it.
  `PhotonHost.CanGoBack` says up front whether back has anywhere to go.
- **The Android activity answers back.** From Android 13 it registers an `OnBackInvokedCallback` only
  while `CanGoBack` is true, so Android keeps its predictive "leave the app" animation for the rest;
  before 13, and wherever the platform still sends back as a key, `DispatchKeyEvent` takes
  `KEYCODE_BACK` on the same condition.
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
- The native generator: the launcher activity's `ConfigurationChanges`.
- The component library: `ListDetail`, and its transpiled twin in the runtime.
- Not reached: eqc, the web realizer, the iOS, macOS and Windows shells, the SDKs and the templates. The
  developer surface does not move.
- Out of scope, and unchanged: the process being killed in the background, which restoring state
  across process death belongs to (#196); back from the first screen with nothing open, which is the
  system's and on Android 16 finishes the activity, so the next launch starts fresh.
