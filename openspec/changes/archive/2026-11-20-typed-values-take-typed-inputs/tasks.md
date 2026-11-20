## 1. The kind of a text entry (#1761)

- [x] 1.1 `TextEntryKind` and `TextEntry.Kind` in Primitives, the enum's union and the runtime's vocabulary twin
- [x] 1.2 The web lowering, C# and its TypeScript twin, with the realizer tests and the vitest cross-pin
- [x] 1.3 Photon: the session carries the kind, and the iOS and Android shells describe it
- [x] 1.4 `TextInput` and `FormInput` take `kind`, `prefix` and `suffix`; the factories mirror them
- [x] 1.5 Proved: the kinds' keyboards in a browser (`inputmode`, and `setSelectionRange` refused by `type="email"`), the session's kind on Photon

## 2. NumberFormatInfo in the browser (#1765)

- [x] 2.1 eqc writes `$eq.numberFormat()` for the four reads; `PlatformMemberRead` carries the scalar members
- [x] 2.2 The runtime answers the culture's data, the invariant one, and the Intl fallback
- [x] 2.3 Proved by the conformance suite in the invariant culture, en-US, pt-PT, nl-NL and de-CH, and the refusals by the compiler's tests

## 3. MoneyInput and NumberInput (#1764)

- [x] 3.1 `Currency`, `MoneyInput`, `NumberInput` and their factories
- [x] 3.2 Their reading and writing proved in .NET and in the browser (the same C#, transpiled)
- [x] 3.3 The web realizer tests, the vitest suite driving the transpiled components, and Photon's
- [x] 3.4 The dashboard sample shows them; looked at in a browser at 390 px and on the desktop, hydration included

## 4. Documentation

- [x] 4.1 The wiki's components and forms pages in all five languages; docs/FLUTTER-PARITY.md
- [x] 4.2 A ledger entry under docs/ledger/ citing #1761, #1764 and #1765
- [x] 4.3 The public API declared, the served runtime's delta written
