# Design

## Context

- A device capability is an interface in `eQuantic.UI.Primitives/Devices`, taken through a constructor.
  eqc resolves it by its simple name (`$eq.services.resolve('IBiometrics')`), and the runtime registers a
  realization under that name in `shared/devices/register.ts`. Nothing in the compiler knows any one
  capability, so a new one costs no compiler change.
- The server render and server actions resolve capabilities from the request's services, where
  `AbsentCapabilities` registers an unavailable stand-in for each device a server has none of.
- A C# `ValueTask<T>` is a JavaScript `Promise<T>`; a record crosses as a plain object with camelCase
  members, an enum as its camelCase name. Web realizations resolve, and never reject (`location.ts`,
  `camera.ts`).
- `WebBiometrics` already decided that WebAuthn is not a biometric check (see proposal.md, Why).

## Goals / Non-Goals

**Goals:**
- One contract a component calls the same way on every host, realized in the browser now.
- No model of the Web Authentication options in C#: the SDK relays them.

**Non-Goals:**
- The Photon shells. Apple's `ASAuthorization`, Android's Credential Manager and Windows Hello each get an
  issue under #774; the Apple one also needs an array-valued entitlement (`webcredentials:` in associated
  domains), which `PhotonEntitlementsBuilder` cannot write yet.
- Conditional mediation (a passkey offered in a field's autofill) and the related-origins request.
  Both are a mode of `AuthenticateAsync` that can widen its signature later.
- Verifying anything. The relying party verifies; the device only creates and signs.

## Decisions

### A contract of its own, not a mode of `IBiometrics`
`IBiometrics` answers whether the person holding the device owns it, and hands back nothing. A passkey is a
key pair registered with a server, used to sign that server's challenge, and its answer is the proof.
Folding one into the other would give an interface whose methods fail in ways its contract cannot describe,
which is the reason `WebBiometrics` gives for answering `Unavailable`.

How Flutter does it: it does not. The framework has no passkey API; apps reach the platform through
community plugins over method channels (`passkeys`, `credential_manager`). docs/FLUTTER-PARITY.md §7 already
records the SDK's answer to method channels, a typed contract per capability, and this adds the eighteenth.

### The relying party's JSON in and out, not C# types
The options carry a challenge, a relying party, a user, the algorithms, the credentials to exclude or allow,
the authenticator's selection, hints and extensions, and the Web Authentication spec keeps adding members.
Typed in C#, the SDK would track that spec field by field, and an app would map its server's JSON into the
types and the answer back to JSON for nothing: the server writes the options as JSON and verifies the answer
as JSON (fido2-net-lib, SimpleWebAuthn, py_webauthn all do). Android's Credential Manager takes the same
JSON as is, and the Apple realization maps it once, inside the SDK. A string in and a string out keeps the
app ignorant of the protocol, which is the point of relaying it.

Alternative considered: typed options with a JSON escape hatch. Rejected, since the hatch would be the path
everyone takes.

### An outcome, like `BiometricResult`, carried with the credential in a record
`PasskeyResult(PasskeyOutcome Outcome, string? Credential)`. A `string?` alone would conflate the five
endings. The outcomes follow what Web Authentication itself distinguishes: `NotAllowedError` (and an
`AbortError` from the page's own token) is `Cancelled`, since the spec deliberately reports a refusal and a
timeout alike; `InvalidStateError` on creation is `AlreadyRegistered`; a host without the API is
`Unavailable`; anything else, unparsable options included, is `Failed`.

### The browser realization
- `IsAvailable`: `window.isSecureContext` and `PublicKeyCredential` defined.
- Options: parsed from the JSON, unwrapped from `publicKey` when present, and turned into the API's types
  with `PublicKeyCredential.parseCreationOptionsFromJSON` and `parseRequestOptionsFromJSON` where the browser
  has them (current Chrome, Edge, Firefox and Safari), and by decoding the base64url members by hand
  otherwise (the challenge, `user.id`, and each listed credential's `id`).
- Answer: `credential.toJSON()` where the browser has it, and the same members encoded by hand otherwise.
- The cancellation token drives an `AbortController` whose signal goes to the ceremony. No device
  realization took a token before this one: the runtime's twin (`utils/cancellation.ts`) carries
  `register`, so the realization registers the abort on it and lets the registration go when the
  ceremony ends, and a token the call omitted, `undefined` on this side, cancels nothing.

### The outcome's zero is `Unavailable`
`default(PasskeyResult)` is a result nobody filled in, and with `Succeeded` as the enum's zero it would
read as a success with no credential, which is the one wrong answer a sign-in must never get by
accident. `BiometricResult` puts `Succeeded` first; this contract does not follow it there.

The zero has to hold in the browser too. The twin's constructor defaults to `'unavailable'` and no
credential, and the struct carries `[ZeroConstructs]` naming that constructor, so eqc writes
`default(PasskeyResult)` as `new PasskeyResult()` rather than `undefined`, which a page would fail on at
its first read. The twin hashes by value (`hashesByValue`), as every twin of a vocabulary record does,
so two equal results compare and hash alike on both sides.

### The options are asked for before the press
Safari runs a Web Authentication ceremony only inside a gesture the person made, and a page that awaits
a server action between the press and `CreateAsync` can spend it: Safari then refuses with
`NotAllowedError`, the same error a person who backed out produces, so the page reads `Cancelled`. The
pattern the documentation and the sample show is the one the relying party's flow allows anyway: the
page asks a server action for the options when the screen opens, keeps them, and the press calls the
ceremony at once. A challenge is good for one ceremony, so the page asks for fresh options after each
one.

## Risks / Trade-offs

- [The JSON is not checked by the SDK] → A malformed option is the server's bug and answers `Failed`, as
  the browser would refuse it. The SDK checks only that it is JSON.
- [Older browsers lack the JSON helpers] → The hand-written conversion covers them, and the tests run both
  paths over a stubbed API.
- [A real ceremony needs an authenticator] → The browser check uses Chrome's virtual authenticator, which
  creates and signs without hardware.
- [Safari's gesture rule] → Measured nowhere automatically: Safari offers no virtual authenticator. The
  documented pattern keeps the ceremony inside the press, and the documentation says `Cancelled` can mean
  a spent gesture in Safari, where a page that does not follow the pattern will see it.

## Migration Plan

Additive. A release that carries it lets an app take `IPasskeys?`; an app that does not is unchanged.
