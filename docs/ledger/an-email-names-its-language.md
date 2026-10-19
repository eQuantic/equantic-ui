- **2026-10-19 · A message names its culture**: `EmailRenderer.Render` takes the message's culture as
  a `CultureInfo`, names it on `<html lang>` and renders the tree under it, so the numbers, dates and
  looked-up texts its components write are in the language the attribute names, where every message
  said `en`. The markup stays invariant in every culture, a message given no culture or the invariant
  one says English, and the caller's cultures are back after the render, a failed one included.
  eQuantic.Auth, which sends its e-mails in English and Portuguese, rewrote the attribute after
  rendering until now ([#942](https://github.com/eQuantic/equantic-ui/issues/942)). Proposed and
  archived through OpenSpec (`openspec/specs/email-realizer`, a new spec).
