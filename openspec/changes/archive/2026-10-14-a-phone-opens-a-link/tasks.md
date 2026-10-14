# Tasks

## 1. The contract

- [x] 1.1 `IUrlLauncher` in Primitives with `OpenUrlAsync` and `CanOpen`; `IWorkspace` keeps `Reveal` and `OpenFile`. Verify: the public API declared, every shell and sample built
- [x] 1.2 `UrlLauncher` in Hosting: a relative URL throws, the policy before the platform with its refusal logged, the absolute form handed over, `CanOpen` under the same policy and silent. Verify: `UrlLauncherTests`, run again with the system asked before the policy and failing three cases

## 2. The shells

- [x] 2.1 macOS (`NSWorkspace`), Windows (`ShellExecuteEx`, the association query), iOS (`UIApplication`), Android (a VIEW intent), each registered. Verify: `MacUrlLauncherTests` against the real NSWorkspace, `WindowsShellTests` on Windows, and the devices below
- [x] 2.2 `MacWorkspace` and `WindowsWorkspace` keep only the file doors, sharing `ShellOpen` on Windows. Verify: `WindowsShellTests`

## 3. The build

- [x] 3.1 The generator declares each opened scheme as `PhotonOpenedScheme`, through the rule the builder uses, and reports `EQ3007` for a scheme built at run time on a phone. Verify: `OpenedSchemeDeclarationTests`, run again with a named door left out of the rule and failing
- [x] 3.2 `eqicon capabilities` writes `LSApplicationQueriesSchemes`; `eqicon queries` writes the Android fragment with the web's schemes, only when it changed. Verify: `PhoneLinkQueriesTests`, run again without the web's schemes and failing
- [x] 3.3 The native SDK writes the fragment before the manifest merge, adds it as an `AndroidManifestOverlay`, removes a merged manifest older than it, and requires the tool. Verify: the shipped APK's manifest, and an added and a removed line built twice each, with and without the stale-manifest target

## 4. The devices

- [x] 4.1 Android emulator (API 36): `tel`, `sms`, `mailto`, `geo` and `https` open the dialer, Messages, Gmail, Google Maps and Chrome; a scheme the app does not open and one nothing claims answer false; built without the `queries` element, every `CanOpen` answers false
- [x] 4.2 iOS 27 simulator: the built `Info.plist` lists the schemes; `https`, `sms`, `maps://` and `https://maps.apple.com` open Safari, Messages and Maps; `tel` and `mailto` answer false (no Phone and no Mail app there); with another app installed that claims `eqbench`, `CanOpen` answers true here and false with main's manifest writer

## 5. Documentation and the suites

- [x] 5.1 The wiki's Capabilities page in English, Portuguese, Spanish, French and German (the contract's row, the sections on a file and on a link, the phones, the table of shells) and the Diagnostics page in English and Portuguese, on the wiki branch named like this pull request. Verify: `WikiShellRealizationsTests`, which fails on the wiki's master naming `IUrlLauncher` in every language
- [x] 5.2 `docs/DIAGNOSTICS.md` (`EQ3007`), `docs/PHONE-CAPABILITIES-PLAN.md`, `docs/platform-catalog/ios.md`, `docs/FLUTTER-PARITY.md`, and one entry under `docs/ledger/` citing #778
- [x] 5.3 The Native.Engine and Web suites and the compiler's coverage guards, each alone and read by its exit code
