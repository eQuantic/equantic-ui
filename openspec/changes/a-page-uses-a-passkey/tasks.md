# Tasks

## 1. The contract

- [x] 1.1 `IPasskeys`, `PasskeyResult` and `PasskeyOutcome` (`Unavailable` its zero) in `eQuantic.UI.Primitives/Devices`, one type per file, in `PublicAPI.Unshipped.txt`; the enum appended to `enum-values.baseline.txt` and `enums.generated.ts`, and the record's TypeScript twin in `shared/primitive-values.ts` with its fixture regenerated, hashing by value and building the zero (`[ZeroConstructs]`); verify with `EnumValueAbiTests`, `EnumUnionsTsGeneratorTests`, `PrimitivesRuntimeExportTests`, `ValueTwinHashTests`, `StructDefaultTests` and `OneTypePerFileTests`.
- [x] 1.2 An absent `IPasskeys` for the server render and server actions (`AbsentCapabilities`, registered in `UIExtensions`); verify with a test in `RequestScopedDependencyTests` that a page taking one renders and that `AuthenticateAsync` answers `Unavailable`.

## 2. The browser

- [x] 2.1 `WebPasskeys` in `shared/devices/passkeys.ts`, registered as `IPasskeys` in `register.ts`: availability from a secure context and the API, the options parsed (unwrapped from `publicKey`) and turned into the API's types through the browser's JSON helpers or by hand, the answer from `toJSON()` or by hand, each refusal mapped to its outcome, and the token aborting the ceremony; verify with `passkeys.spec.ts` over a stubbed `navigator.credentials`, both with and without the JSON helpers, for every scenario of the spec.
- [x] 2.2 A sample page that creates a passkey and then signs with it through `IPasskeys`, the options written by a server action the page calls when it opens, so each press runs the ceremony at once; verify in a real Chrome with a virtual authenticator (over the DevTools protocol): `CreateAsync` answers `Succeeded` with a registration whose client data carries the challenge, `AuthenticateAsync` answers `Succeeded` with an assertion, a second creation that excludes the first credential answers `AlreadyRegistered`, and the page served over plain HTTP from a non-local host answers `Unavailable`.

## 3. Documentation

- [x] 3.1 The wiki's Capabilities page, in English, Portuguese, Spanish, French and German, on a wiki branch named like this pull request's: the contract's row, and a section on relaying the relying party's JSON with a server action, asked for before the press, with what `Cancelled` can mean in Safari; verify with the wiki guards against that branch.
- [x] 3.2 `docs/FLUTTER-PARITY.md` (eighteen typed capabilities), the platform catalog's passkey rows on iOS and Android (covered by the contract, realized on the web), `docs/PHONE-CAPABILITIES-PLAN.md`, and one entry under `docs/ledger/` citing #1017; and one issue per shell under #774 (Apple, Android, Windows); verify with `LedgerTests` and the docs tests.
