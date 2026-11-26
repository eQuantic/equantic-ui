## Why

Closes #1881, a feature under #288 (the roadmap ahead).

The browser gives every Server Action 30 seconds: `ServerActionsClient.invoke` aborts the request after its
`timeoutMs`, 30000 unless told otherwise, and the page's `await` throws "Server action timed out". The runtime
has always taken the option, but eqc wrote every stub as `invoke('<id>', [args])`, so no app could change it.
Nothing on the server limits an action: the browser alone gives up.

A real site met it. An action that waits for an AI assistant to write a proposal took 99.6 s in production;
the page's `await` threw, its "writing…" state stayed for good, and the server finished the work and wrote its
reply to a request the browser had already let go. Open-ended work belongs behind a ticket and server events,
which that site now does; a slow but bounded action (an OAuth code checked with a provider, up to 90 s) should
not need that machinery.

## What Changes

- `[ServerAction(TimeoutSeconds = 120)]`: an action states how long the browser waits for it. Unset, or 0,
  keeps the 30 seconds.
- eqc reads the value as C# reads an attribute's argument, a constant (`2 * 60` and a `const` included), and
  writes `invoke('<id>', [args], { timeoutMs: 120000 })` for an action that states one; an action that states
  none keeps its shape, byte for byte.
- A value the stub could not honour is refused where it is written, EQ2017: below 0, above 600, or not a
  constant the build can read.
- For a developer using the SDK: one property on the attribute, read at build time. The docs say what holds
  on the other side (nothing on the server limits an action; a reverse proxy may end a request sooner).

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `server-actions`: gains the requirement "A Server Action states how long the browser waits for it".

## Impact

- **Primitives**: `ServerActionAttribute.TimeoutSeconds`.
- **eqc**: the parser reads it, `ComponentCompiler` refuses it out of range (EQ2017), the emitter hands it to the
  stub's `invoke`.
- **The runtime**: none; `invoke` already takes `timeoutMs`. Its spec now pins it.
- **The server, the realizers, the Photon shells, the templates**: none. Photon has no Server Actions.
