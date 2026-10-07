## Why

Inside `namespace Falei.Web.Chat`, `Portal.Fold.Text(n)`, which names `Falei.Web.Portal.Fold`, was
written `Fold.text(n)` with no import, and the page threw `ReferenceError: Fold is not defined`, while
`Fold.Text(n)` under a using worked (#625). The strategy that strips a namespace returned the type's
name and never said the type was introduced, so the import was decided by the name as written. #479
fixed the same shape for a base class written with its namespace, and this is the expression's case.

## What Changes

- A type reached through its namespace, part of it, the whole of it or `global::`, is registered as
  introduced by the symbol the name binds, so the module imports it as it imports a type reached
  through a using, a class's and a static class's alike. A namespace reached through another is only
  stripped, as before.

## Capabilities

### New Capabilities

### Modified Capabilities

- `transpiler-names`: a type reached through its namespace is imported by the symbol it binds.

## Impact

- `NamespaceRemovalStrategy`. No public API change and no developer surface change.
