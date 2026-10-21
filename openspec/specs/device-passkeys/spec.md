# device-passkeys Specification

## Purpose
What creating and using a passkey answers: on the web, what each outcome means and what the JSON carries
in and out, and what a host without passkeys answers, so a component writes the same C# wherever it runs.

## Requirements

### Requirement: A page creates a passkey from the relying party's options
`IPasskeys.CreateAsync` SHALL take the relying party's creation options as the W3C Web Authentication JSON
(`PublicKeyCredentialCreationOptionsJSON`, alone or under a `publicKey` member), create a passkey with the
browser's authenticators, and answer `Succeeded` with the new credential as that same JSON
(`RegistrationResponseJSON`: its binary members in base64url, its `response.clientDataJSON` and
`response.attestationObject` included). It SHALL never throw for what the browser refused: every refusal
is an outcome.

#### Scenario: A passkey is created
- **WHEN** a page passes creation options with a challenge, a relying party and a user, and the authenticator creates the credential
- **THEN** the answer is `Succeeded`, and its credential is JSON whose `id`, `rawId`, `response.clientDataJSON` and `response.attestationObject` are base64url and whose client data carries the options' challenge

#### Scenario: Options wrapped in `publicKey`
- **WHEN** a page passes the same options under a `publicKey` member
- **THEN** the passkey is created from them exactly as from the unwrapped options

#### Scenario: The device already holds a passkey for the account
- **WHEN** the options exclude a credential the authenticator holds, and the browser refuses the creation for it
- **THEN** the answer is `AlreadyRegistered`, with no credential

### Requirement: A page signs with a passkey from the relying party's options
`IPasskeys.AuthenticateAsync` SHALL take the relying party's request options as the W3C Web Authentication
JSON (`PublicKeyCredentialRequestOptionsJSON`, alone or under a `publicKey` member), sign with a passkey,
and answer `Succeeded` with the assertion as that same JSON (`AuthenticationResponseJSON`: its
`response.clientDataJSON`, `response.authenticatorData`, `response.signature` and, when the authenticator
gives it, `response.userHandle`, in base64url).

#### Scenario: A passkey signs
- **WHEN** a page passes request options with a challenge and the person uses a passkey
- **THEN** the answer is `Succeeded`, and its credential is JSON carrying the signature and the authenticator data in base64url, with client data carrying the options' challenge

### Requirement: Every way a ceremony ends is an outcome
A ceremony SHALL answer `Cancelled` when the person backed out or the browser let it lapse (Web
Authentication reports both alike, on purpose), `AlreadyRegistered` as above, `Unavailable` when this host
cannot create or use a passkey, and `Failed` for any other refusal, each with no credential. A cancellation
token cancelled before the ceremony ends SHALL abort it and answer `Cancelled`.

#### Scenario: The person backs out
- **WHEN** the person dismisses the browser's prompt
- **THEN** the answer is `Cancelled`, with no credential

#### Scenario: Options that are not JSON
- **WHEN** a page passes text that is not the options' JSON
- **THEN** the answer is `Failed`, with no credential, and nothing is asked of the person

#### Scenario: Cancelled by the page
- **WHEN** the page cancels the token while the prompt is open
- **THEN** the ceremony is aborted and the answer is `Cancelled`

#### Scenario: A result nobody filled in
- **WHEN** a component reads a `PasskeyResult` it never received, its default
- **THEN** its outcome is `Unavailable` and it carries no credential, never `Succeeded`

### Requirement: A host without passkeys says so
`IsAvailable` SHALL be `true` only where the host can run a ceremony: in a browser, a secure context with
the Web Authentication API. Where it is `false`, both methods SHALL answer `Unavailable` without asking
anything of the person. The server render SHALL resolve an `IPasskeys` whose `IsAvailable` is `false`, so a
component that takes one renders on the server as it does where it is missing.

#### Scenario: A page served over plain HTTP from another host
- **WHEN** a page outside a secure context calls `CreateAsync`
- **THEN** `IsAvailable` is `false` and the answer is `Unavailable`

#### Scenario: The server render
- **WHEN** a page that takes an `IPasskeys` is rendered on the server
- **THEN** it receives one whose `IsAvailable` is `false`, and `AuthenticateAsync` answers `Unavailable`
