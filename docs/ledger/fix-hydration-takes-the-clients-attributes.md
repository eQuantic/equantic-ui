- **2026-11-11 · Hydration gives the server's elements the browser's attributes and text**: a page
  that decided an attribute from a capability's availability kept the server's answer for good. The
  server resolves every capability to its absent realization, so a button gated on
  `IFileSaver.IsAvailable` was served disabled, and hydration adopted the element as it was while
  keeping the client's tree as the one the DOM reflected, so no later render ever corrected it. The
  reconciler's `hydrate` now diffs each adopted element's attributes against the client's with the
  render's own `updateAttributes`, keeps the framework's `data-eq-*` markers the client does not
  name, and writes the client's text over a text node. Measured on the dashboard sample's
  `/recovery-codes` in a browser: served `disabled`, enabled after hydration, and still disabled with
  the old runtime ([#1302](https://github.com/eQuantic/equantic-ui/issues/1302)). Proposed and
  archived through OpenSpec (`openspec/specs/hydration-contract`).
