# Proposal

Closes #1228, a Feature under #898 (1.0: the web SDK stable, with the native track experimental).
Met by an app: myartwork3d frames its checkout in a `WebFrame`, and Apple Pay and Google Pay stay
unavailable there.

## Why

`WebFrame` says what the framed document may do only through `WebSandbox` (`Scripts`, `SameOrigin`,
`Forms`, `Popups`), which the web realizer writes into the iframe's `sandbox`. A browser gates a
second set of capabilities by delegation, the iframe's `allow` (Permissions Policy), and refuses
each one to an embedded document of another origin until the page hands it over. The vocabulary had
no word for any of them, so a payment provider's checkout offered the card and never the wallets,
because the Payment Request API inside a cross-origin frame needs `allow="payment"`. The same wall
stood in front of a framed video call, a framed sign-in with a passkey, a map that asks for the
location, a player that goes fullscreen and a widget that copies. The only way past it was
`HtmlElement("iframe")` with hand-written attributes: the escape hatch as the primary path, and
web-only for good.

## What Changes

- `WebSandbox` gains seven flags, appended: `Payment`, `Camera`, `Microphone`, `Location`,
  `Fullscreen`, `CopyToClipboard` and `Passkeys`, named with the words the device contracts already
  use. One set of flags says what the document may do, whichever attribute the browser reads it
  from, so an app states it in one place.
- The web realizer writes the four that loosen the isolation into `sandbox`, as before, and the
  seven that hand the document something of the page's into `allow` (`payment`, `camera`,
  `microphone`, `geolocation`, `fullscreen`, `clipboard-write`, and `publickey-credentials-get;
  publickey-credentials-create` for a passkey). A frame handed none writes no `allow`.
- The runtime's lowering writes the same two attributes from the same flags.
  `web-frame-attributes.fixture.json`, written by the C# realizer's test, pins every flag alone, the
  default, the locked frame and every flag at once, and `lowering.spec.ts` asserts the browser's half
  against it. A flag in neither of the realizer's tables fails a test derived from the enum.
- The wiki's Components page documents `WebFrame`, its flags and what each hands over, in all five
  languages.

Photon has no surface for a web document yet. When one lands it realizes the same flags.

## Impact

- `eQuantic.UI.Primitives`: seven members appended to `WebSandbox` (an addition, no break)
- `eQuantic.UI.Web` and `src/eQuantic.UI.Runtime`: the frame's `allow`
- `openspec/specs/embedded-documents`, a new spec
- The wiki's Components page, and an entry under `docs/ledger/`
