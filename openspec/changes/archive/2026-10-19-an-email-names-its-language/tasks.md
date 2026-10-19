# Tasks

## 1. The message's culture

- [x] 1.1 `EmailRenderer.Render` takes an optional `CultureInfo culture`, renders both parts under it and restores the caller's cultures in a `finally`
- [x] 1.2 The shell writes the culture's name on `<html lang>` through `EmailRealizer.EscapeAttribute`, `en` for none and for the invariant culture
- [x] 1.3 `PublicAPI.Unshipped.txt` removes the old signature and declares the new one
- [x] 1.4 Pin it in `MessageLanguageTests`, executed against the realizer: the name on the document, English by default, the tree's numbers and dates in three cultures, identical markup in six, the ambient cultures restored after a message and after a failed one

## 2. Documentation

- [x] 2.1 The wiki's EmailRealizer page, in every language it is written in, shows the culture
- [x] 2.2 One entry for the ledger citing #942, as a file of its own under docs/ledger/
