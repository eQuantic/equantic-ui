# Proposal

Closes #777, a Feature under #774 (Track D: a phone's capabilities in C#), and #872, a Bug under
#196 found on the way.

## Why

`IDeepLinks` was realized on macOS and Windows. Neither phone shell registered it, so a link that
opened the app on a phone landed on its front door and the URL was dropped. The declaration half
was missing too: `builder.Bundle.UrlScheme("acme")` reached only the Mac's bundle, so on a phone
nothing told the system the app answered that scheme.

And on iOS nothing an app declared reached its `Info.plist` at all (#872). The SDK wrote the partial
manifest after the compiler and handed it to Apple's packaging there, by which time the app's
manifest had been compiled: the Wallet sample declared its Face ID and photo library sentences in
C#, built green, and carried neither key.

## What Changes

- **One declaration, three systems.** The same `builder.Bundle.UrlScheme(…)` now writes the iOS URL
  type (`CFBundleURLTypes`, in the partial manifest the SDK already writes from the compiled
  assembly) and the Android link filter (on the launcher activity the generator writes), beside the
  Mac's bundle.
- **Both phone shells feed a relay.** Android reads the data of the intent that started the
  activity and of each intent it is handed while it runs. iOS reads the URL contexts of the scene's
  connection and `scene:openURLContexts:`.
- **`Launch` is exact on a phone.** A phone is handed the link with the launch, so its shell tells
  the relay what the app was started with (`DeepLinkRelay.StartedWith`): an app opened from its icon
  answers `null`, a link that arrives later is heard and does not become it, and the first tree
  already reads it.
- **The Android launcher is a single task**, so a link opened by another app reaches the running
  activity instead of starting a second one over it.
- **What an iPhone app declares reaches its Info.plist** (#872). The manifest target runs where the
  iOS SDK collects partial manifests, and a file left by an earlier build is removed when nothing is
  declared any more.

For a developer: nothing new to write. `builder.Bundle.UrlScheme("acme")` and the two lines that
read `IDeepLinks` work on a phone as they do on a desktop.

## Capabilities

### New Capabilities

- `deep-links`: what a declared scheme writes on each system, what `Launch` answers and who hears a
  link, on the phones.

### Modified Capabilities

None.

## Impact

- **The vocabulary** (`eQuantic.UI.Primitives`): `DeepLinkRelay.StartedWith`.
- **The generator** (`eQuantic.UI.Native.Generators`): the launcher activity's link filter and
  launch mode.
- **The build tool** (`eQuantic.UI.Native.Build`, `eqicon`): the `capabilities` verb writes the URL
  types and takes the app's identifier.
- **The native SDK** (`eQuantic.UI.Sdk.Native`): when `EQuanticCapabilityManifest` runs.
- **The phone shells** (`eQuantic.UI.Native.Shell.Android`, `eQuantic.UI.Native.Shell.iOS`).
- **Public surface moves** by additions only: `DeepLinkRelay.StartedWith`, and the overrides
  `PhotonActivity.OnNewIntent` and `PhotonSceneDelegate.OpenUrlContexts`. The developer surface does
  not move. Nothing breaks.
