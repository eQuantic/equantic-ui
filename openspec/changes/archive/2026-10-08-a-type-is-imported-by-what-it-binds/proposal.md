## Why

Inside `namespace Falei.Web.Chat`, `Portal.Fold.Text(n)`, which names `Falei.Web.Portal.Fold`, was
written `Fold.text(n)` with no import, and the page threw `ReferenceError: Fold is not defined`, while
`Fold.Text(n)` under a using worked (#625). The strategy that strips a namespace returned the type's
name and never said the type was introduced, so the import was decided by the name as written. #479
fixed the same shape for a base class written with its namespace, and this is the expression's case.
The review of this change found the same defect through a using alias: `using F = Falei.Web.Portal.Fold;`
then `F.Text(n)` wrote `F.text(n)`, and `new F()` wrote `new F()`, names nothing defines.

## What Changes

- A type reached through its namespace, part of it, the whole of it or `global::`, is registered as
  introduced by the symbol the name binds, so the module imports it as it imports a type reached
  through a using, a class's and a static class's alike. A namespace reached through another is only
  stripped, as before.
- A type reached through a using alias, read or built, is written by its own name and registered as
  introduced, so `F.Text(n)` is `Fold.text(n)` and `new F()` is `new Fold()`, each importing `Fold`.
- An enum or an interface reached either way registers nothing, having no JavaScript value, and a
  host-only type is left to the strategies that refuse it however it is spelled.

## Capabilities

### New Capabilities

### Modified Capabilities

- `transpiler-names`: a type reached through its namespace or a using alias is written by its own name
  and imported by the symbol it binds.

## Impact

- `NamespaceRemovalStrategy`, `IdentifierStrategy` and `ObjectCreationStrategy`. No public API change and
  no developer surface change.
