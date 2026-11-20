## 1. The kind of a text entry (#1761)

- [ ] 1.1 `TextEntryKind` and `TextEntry.Kind` in Primitives, the enum's union and the runtime's vocabulary twin
- [ ] 1.2 The web lowering, C# and its TypeScript twin, with the realizer tests and the vitest cross-pin
- [ ] 1.3 Photon: the session carries the kind, and the iOS and Android shells describe it
- [ ] 1.4 `TextInput` and `FormInput` take `kind`, `prefix` and `suffix`; the factories mirror them
- [ ] 1.5 Proved: the kinds' keyboards in a browser (`inputmode`, and `setSelectionRange` refused by `type="email"`), the session's kind on Photon

## 2. NumberFormatInfo in the browser (#1765)

- [ ] 2.1 eqc writes `$eq.numberFormat()` for the four reads; `PlatformMemberRead` carries the scalar members
- [ ] 2.2 The runtime answers the culture's data, the invariant one, and the Intl fallback
- [ ] 2.3 Proved by the conformance suite in the invariant culture, en-US, pt-PT, nl-NL and de-CH, and the refusals by the compiler's tests

## 3. MoneyInput and NumberInput (#1764)

- [ ] 3.1 `Currency`, `MoneyInput`, `NumberInput` and their factories
- [ ] 3.2 Their reading and writing proved in .NET and in the browser (the same C#, transpiled)
- [ ] 3.3 The web realizer tests, the vitest suite driving the transpiled components, and Photon's
- [ ] 3.4 The dashboard sample shows them; looked at in a browser at 390 px and on the desktop, hydration included

## 4. Documentation

- [ ] 4.1 The wiki's components and forms pages in all five languages; docs/FLUTTER-PARITY.md
- [ ] 4.2 A ledger entry under docs/ledger/ citing #1761, #1764 and #1765
- [ ] 4.3 The public API declared, the served runtime's delta written
