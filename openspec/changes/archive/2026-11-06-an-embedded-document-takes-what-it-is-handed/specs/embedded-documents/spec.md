## ADDED Requirements

### Requirement: An embedded document is handed what its page may use by name

`WebSandbox` SHALL name, beside the flags that loosen the frame's isolation (`Scripts`, `SameOrigin`,
`Forms`, `Popups`), the capabilities a page may hand an embedded document: `Payment`, `Camera`,
`Microphone`, `Location`, `Fullscreen`, `CopyToClipboard` and `Passkeys`. On the web a `WebFrame`
SHALL write the flags that loosen its isolation into the iframe's `sandbox`, which is always present,
and the capabilities it is handed into its `allow`, which SHALL be absent when it is handed none.
Every flag SHALL be written in exactly one of the two attributes.

#### Scenario: A checkout that offers the wallet

- **WHEN** a page frames a payment provider's checkout with `Scripts | SameOrigin | Forms | Popups | Payment`
- **THEN** the iframe's `sandbox` is `allow-scripts allow-same-origin allow-forms allow-popups` and its `allow` is `payment`

#### Scenario: A frame handed nothing of its page's

- **WHEN** a frame keeps the default `Scripts`
- **THEN** its iframe has no `allow`

#### Scenario: A passkey

- **WHEN** a frame is handed `Passkeys`
- **THEN** its `allow` is `publickey-credentials-get; publickey-credentials-create`

### Requirement: The server and the browser write a frame alike

The server's lowering of a `WebFrame` and the runtime's SHALL write the same `sandbox` and `allow`
for every value of `WebSandbox`, pinned by one fixture that the server's half writes and the
browser's half reads, so that a capability is never handed over before hydration and taken away
after it, or the other way round.

#### Scenario: A flag one half does not spell

- **WHEN** the runtime's lowering leaves out a flag the server's writes
- **THEN** `lowering.spec.ts` fails, naming the flag's value
