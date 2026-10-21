- **2026-10-21 · A page uses a passkey, from one C# contract**: D35 of Track D. `IPasskeys` creates a
  passkey from the relying party's creation options and signs with one from its request options, both
  the W3C Web Authentication JSON in and out, so an app relays its server's text to the device and the
  device's back without learning a field of either. Each ceremony answers a `PasskeyResult` whose
  `PasskeyOutcome` says how it ended (`Succeeded`, `Cancelled`, `AlreadyRegistered`, `Failed`, and
  `Unavailable`, the zero, so a result nobody filled in never reads as a sign-in), and a cancellation
  token aborts it. The browser realizes it over `navigator.credentials` in a secure context, through
  its own JSON conversions where it has them and the members every ceremony needs where it does not;
  the server render answers unavailable. The documented pattern asks the server for the options when
  the screen opens, because Safari runs a ceremony only inside the person's gesture and a round trip
  between the press and the call can spend it. The sample's `/passkeys` page creates a passkey, signs
  in and is refused a second one in a real Chrome over a virtual authenticator, kept as
  `tests/e2e/tests/passkeys.spec.ts` ([#1017](https://github.com/eQuantic/equantic-ui/issues/1017);
  Apple, Android and Windows follow in [#1018](https://github.com/eQuantic/equantic-ui/issues/1018),
  [#1019](https://github.com/eQuantic/equantic-ui/issues/1019) and
  [#1020](https://github.com/eQuantic/equantic-ui/issues/1020)). Proposed and archived through
  OpenSpec (`openspec/specs/device-passkeys`, a new spec).
