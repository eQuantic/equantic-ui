- **2026-10-30 · A field shows what the page holds**: a Bug ([#1026](https://github.com/eQuantic/equantic-ui/issues/1026)).
  The reconciler compared one tree with the last and never read the element, so a page that reset a field in the same
  render as the typing held `""` both times, wrote nothing, and left the typed text showing over an empty field,
  and a multiline field never showed a reset at all. eQuantic.Auth's sign-in met it on a refused code. A text control
  with an `input` handler is now compared by its live value, as React's controlled rule does. Proposed and archived
  through OpenSpec (`text-entry`).
