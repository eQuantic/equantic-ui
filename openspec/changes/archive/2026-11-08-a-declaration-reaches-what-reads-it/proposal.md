## Why

Closes #779 and #878, bugs under #774 (the phone's capabilities).

`UseContacts`, `UseNotifications`, `UseMicrophone` and `UsePhotoLibraryAdd` wrote `READ_CONTACTS`, `POST_NOTIFICATIONS`,
`RECORD_AUDIO` and `WRITE_EXTERNAL_STORAGE` and their `Info.plist` sentences, and no contract on any target uses them.
App Review and Google Play both reject an app that asks for a permission it does not use, so the SDK handed a
developer a declaration that could only hurt them. And a fact stated on `builder.Bundle` reached the Mac's `Info.plist`
and stopped there: on iOS only the URL schemes travelled, so `builder.Bundle.Key("ITSAppUsesNonExemptEncryption", …)`
built green and the key was not in the app.

## What Changes

- The four builder methods go, and `builder.Capabilities.Use(DeviceCapability.X, …)` for those four fails the build
  with `EQ3008`, naming the capability. The enum members stay, since an enum is append-only.
- A fact on `builder.Bundle` reaches the iPhone's manifest through the partial manifest the schemes use.
  `Category`, `MinimumSystemVersion` and `Agent`, which only a Mac reads, stay out, and the build names each.
- Migration: remove the line that declared one of the four. The declaration comes back with its API.

## Capabilities

### New Capabilities

- `native-manifests`: what an app declares in C# and what reaches each platform's manifest.

### Modified Capabilities

(none)

## Impact

- **The Photon SDK**: `PhotonCapabilitiesBuilder`, the generator, and `eqicon`'s `CapabilityManifest` and `BundleManifest`.
- **Public surface**: `eQuantic.UI.Native.Hosting` (the native track, experimental) loses the four methods.
- **eqc, the runtime, the web realizer, the templates**: none. The developer surface does not move.
