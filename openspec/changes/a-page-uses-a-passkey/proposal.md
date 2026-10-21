# Proposal

Closes #1017 (D35), a Feature under #774 (Track D: a device's capabilities in C#).

## Why

A component cannot use a passkey. The browser's Web Authentication API is out of a component's reach,
and the one contract near it, `IBiometrics`, is a local yes or no that hands back nothing a server can
check, which `WebBiometrics` says in its own comment. So an app that signs people in with passkeys has
no C# to write, and eQuantic.Auth's hosted screens cannot offer one at all (eQuantic/equantic-auth#135):
no passkey sign-in, no passkey as a second factor, no passkey as the way to set one up.

## What Changes

- **A passkey is a contract of its own.** `IPasskeys` in `eQuantic.UI.Primitives`, taken through a
  constructor like every other capability:
  - `IsAvailable`: whether this host can create and use a passkey at all.
  - `CreateAsync(options)`: creates a passkey from the relying party's creation options and answers the
    new credential, for the server to verify and store.
  - `AuthenticateAsync(options)`: signs with a passkey from the relying party's request options and
    answers the assertion, for the server to verify.
- **Both answer how it went, not a boolean.** A `PasskeyResult` carries a `PasskeyOutcome` (succeeded,
  cancelled, already registered, failed, unavailable) and, when it succeeded, the credential. "Did not
  succeed" hides situations an app must tell apart: a person who backed out is not shown an error, and a
  device that already holds a passkey for the account is told so.
- **The options and the credential are the relying party's own JSON.** The W3C Web Authentication JSON
  that every relying-party library writes and verifies, and that Android's Credential Manager takes as
  is. The app hands the server's text to the device and the device's text to the server, and never
  learns a field of either.
- **The browser realizes it**, in a secure context, over the platform's authenticators and security
  keys. The server render answers `Unavailable`, as it answers every device it has none of.

For a developer: take an `IPasskeys?` in a component's constructor, pass the options a server action
answered to `CreateAsync` or `AuthenticateAsync`, and send `result.Credential` back to the server when
`result.Outcome` is `Succeeded`. No JavaScript, no base64, no `ArrayBuffer`.

Nothing is removed, so no app changes to upgrade.

## Capabilities

### New Capabilities

- `device-passkeys`: what creating and using a passkey answers on the web, what each outcome means, what
  the JSON carries in and out, and what a host without one answers.

### Modified Capabilities

None.

## Impact

- **The vocabulary** (`eQuantic.UI.Primitives`): `IPasskeys`, `PasskeyResult`, `PasskeyOutcome`. The
  public surface grows by these three; the developer surface (`developer-surface.baseline.txt`) moves
  only by them.
- **The runtime** (`eQuantic.UI.Runtime`): `WebPasskeys`, registered as `IPasskeys`; the TypeScript twin
  of `PasskeyResult` and the union of `PasskeyOutcome`, generated like every other.
- **The server** (`eQuantic.UI.Server`): an absent `IPasskeys` for the server render.
- **eqc, the web realizer, the SDKs and the templates**: unchanged. A capability crosses as an interface
  resolved by name, which the compiler already does for every one.
- **The Photon shells**: unchanged in this change. Apple (`ASAuthorization`), Android (Credential
  Manager) and Windows (Windows Hello's WebAuthn) each realize it in an issue of its own.
- **Documentation**: the wiki's Capabilities page in its five languages, `docs/FLUTTER-PARITY.md`, the
  platform catalog's passkey rows, `docs/PHONE-CAPABILITIES-PLAN.md`, and a ledger entry.
