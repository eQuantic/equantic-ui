# Proposal

Closes #942, a Task under #313 (Email, Track M).

## Why

`EmailRenderer.Render` wrote `<html lang="en">` on every message. An app that sends e-mail in more
than one language had no way to say which one a message is in, so screen readers and the clients that
hyphenate or translate read a Portuguese message as English. eQuantic.Auth renders its transactional
e-mails with the realizer (its ADR-0012) in English and Portuguese, and rewrites the attribute after
rendering until this lands.

## What Changes

- `EmailRenderer.Render(tree, theme, preheader, culture)` takes the message's culture as a .NET
  `CultureInfo`, the type .NET names a language with, rather than a tag the developer spells. The
  document names it on `<html lang>` (`pt-BR`), escaped as every attribute is.
- The tree is rendered under that culture, as a page is rendered under its request's culture, so a
  number, a date or a text a component writes in Build is in the language the attribute names. Every
  length and color the realizer writes stays invariant, so the markup is the same in every culture.
- Not given, the message says English, as it always has, and the tree renders under the ambient
  culture. The invariant culture says English too, the language .NET associates with it.
- The ambient culture is the caller's again when the render returns or throws.

## Impact

- `eQuantic.UI.Email`: `EmailRenderer.Render` gains the optional `culture` parameter. The public
  surface moves: the old signature is removed and the new one declared in `PublicAPI.Unshipped.txt`.
  Source written against the old one compiles unchanged, and an assembly compiled against it is
  rebuilt.
- No other part of the SDK changes, and the developer surface outside C# does not move.
- Migration: none for source. An app that rewrote `lang` after rendering passes the culture instead.
