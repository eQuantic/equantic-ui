- **2026-11-06 · An embedded document is handed what its page may use**: a `WebFrame` could loosen
  its isolation (script, origin, forms, new windows) and hand the document nothing of its page's, so
  a payment provider's checkout framed by myartwork3d offered the card and never Apple Pay or Google
  Pay, which a browser refuses a cross-origin frame without `allow="payment"`. `WebSandbox` names
  `Payment`, `Camera`, `Microphone`, `Location`, `Fullscreen`, `CopyToClipboard` and `Passkeys`, and
  both halves of the web target write them into the iframe's `allow`, pinned against one fixture the
  server's half writes and the browser's reads. The wiki's Components page documents `WebFrame` in
  all five languages ([#1228](https://github.com/eQuantic/equantic-ui/issues/1228)). Proposed and
  archived through OpenSpec (`openspec/specs/embedded-documents`, a new spec).
