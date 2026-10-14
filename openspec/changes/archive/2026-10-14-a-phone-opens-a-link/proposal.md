# Proposal

Closes #778 (D11), a Feature under #774 (Track D: a phone's capabilities in C#).

## Why

A phone had no way to hand even an `https` link to the system. Opening a link was two methods of
`IWorkspace`, `OpenUrl` and `CanOpen`, and `IWorkspace` is desktop-only by its own contract: its other
two methods reveal a file in a file browser and hand a path to whatever owns its kind, which mean
nothing on a phone. So neither phone shell registered it, and `builder.Workspace.OpensMail()`,
`OpensPhone()` and `OpensMessages()` widened a policy that only a desktop consulted.

And a phone answers "does something open this link" only for the schemes the app's manifest lists:
`LSApplicationQueriesSchemes` on iOS, `queries` on Android 11 and later. Nothing wrote either, so a
phone realization would have answered `CanOpen` with `false` for every scheme, `https` included on
Android, whatever was installed.

## What Changes

- **A link is a contract of its own.** `IUrlLauncher` carries `OpenUrlAsync(url)` and `CanOpen(url)`,
  and every Photon shell registers one: macOS, Windows, iOS and Android. `IWorkspace` keeps `Reveal`
  and `OpenFile`, the two doors for a file, and stays desktop-only.
- **Opening is asynchronous.** An iPhone says whether it opened the URL in a callback, so the answer
  is a `ValueTask<bool>`; a desktop and Android answer before the call returns.
- **One rule before the platform, written once.** `UrlLauncher` (Hosting) applies what every shell
  must: a relative URL throws, a scheme the app does not open answers `false` and is logged with the
  line that opens it, and only then is the system asked. A shell writes the two calls only it knows.
- **The phones realize it.** iOS over `UIApplication.openURL:options:completionHandler:` and
  `canOpenURL:`, Android over a VIEW intent and its resolution.
- **The build writes what the phones ask about.** The generator reads `builder.Workspace.Opens…` as it
  reads `builder.Bundle`, and writes one `PhotonOpenedScheme` per scheme on the assembly. The iOS
  partial manifest gains `LSApplicationQueriesSchemes` (what the app opens and its own schemes), and
  an Android app gains a `queries` element (the web's two schemes and what it opens), written by
  `eqicon queries` as a fragment the Android build merges over the manifest it generates.
- **A scheme a phone app opens is a constant.** One built at run time cannot reach a manifest written
  at build time, so `Opens(variable)` is `EQ3007` where a manifest is written. A desktop reads the line
  at run time alone and is not told.

For a developer: a phone opens a link, a mail, a call, a message or a map with the same lines that
did it on a desktop, and no `Info.plist` or manifest is opened to make `CanOpen` tell the truth.

**Migration.** `IWorkspace.OpenUrl(url)` and `IWorkspace.CanOpen(url)` are gone. Take an
`IUrlLauncher` where the `IWorkspace` was taken for a link, and write
`await launcher.OpenUrlAsync(url)` and `launcher.CanOpen(url)`.

## Capabilities

### New Capabilities

- `opening-links`: what handing a link to the system answers on every Photon shell, the rule applied
  before the platform, and what a phone's build writes so that the question tells the truth.

### Modified Capabilities

None.

## Impact

- **The vocabulary** (`eQuantic.UI.Primitives`): `IUrlLauncher`; `IWorkspace` loses `OpenUrl` and
  `CanOpen`.
- **Hosting** (`eQuantic.UI.Native.Hosting`): `UrlLauncher`, the base every shell's realization
  derives from, and `PhotonOpenedSchemeAttribute`.
- **The generator** (`eQuantic.UI.Native.Generators`): the opened schemes, and `EQ3007`.
- **The build tool** (`eQuantic.UI.Native.Build`, `eqicon`): `LSApplicationQueriesSchemes` in the
  `capabilities` verb, and the `queries` verb. The code writers (`eQuantic.UI.Codegen`) gain
  `AndroidManifestWriter`.
- **The native SDK** (`eQuantic.UI.Sdk.Native`): the Android fragment written before the manifest
  merge, and a merged manifest older than it rebuilt.
- **The four shells** register `IUrlLauncher`; `MacWorkspace` and `WindowsWorkspace` lose the link
  methods and their constructor's policy and logger.
- **Public surface:** additions (`IUrlLauncher`, `UrlLauncher`, `PhotonOpenedSchemeAttribute`,
  `MacUrlLauncher`, `WindowsUrlLauncher`, `AndroidManifestWriter`) and the breaks above, with the
  workspace constructors that took the policy. The developer surface does not move.
