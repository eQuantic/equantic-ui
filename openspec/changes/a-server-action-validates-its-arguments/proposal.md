## Why

Closes #1984, a Feature under the epic #898 (milestone 1.0.0), found while designing #1966. A Server Action's
arguments reach the method without their DataAnnotations being checked, so an app validates by hand or not at
all. The page's form also never hears a server's verdict without code of the developer's.

## What Changes

For a developer, nothing to write:

```csharp
[ServerAction] public Task Save(Patient patient) => …;      // [Required], [Range], [RegularExpression]… checked first
await form.SubmitAsync(() => Save(model));                  // the server's messages land on the form's fields
```

- **Before it runs**, a Server Action's arguments are validated with .NET's DataAnnotations: parameters' attributes,
  models, nested models and collections.
- **A refusal is `400`** with .NET's `HttpValidationProblemDetails` (`errors` keyed `Address.PostalCode`), in the
  page's culture.
- **The browser throws `ServerValidationException`** (`Errors` as `FieldError`s), catchable in C#, and
  `FormController.SubmitAsync` applies it to the fields.
- **BREAKING (preview):** an action whose arguments carry DataAnnotations now refuses invalid ones before it runs.
  Migration line: drop hand-written validation, or take a type without the attributes where an invalid model must be
  accepted.

## Capabilities

### New Capabilities

- `server-action-validation`: a Server Action validates its arguments before it runs, a refusal reaches the page as
  .NET's validation problem, and a form shows it on its fields.

### Modified Capabilities

None.

## Impact

- **Parts reached:** the Server (`ServerActionsMiddleware`, a new argument validator), Primitives
  (`ServerValidationException`, `FormController`), the runtime (`server-actions.ts` and the transpiled twins), and the
  wiki's server actions and forms pages.
- **Public surface moves:** additions in Primitives' `PublicAPI.Unshipped.txt`. The developer surface does not move.
- **Principles:** 1, 3 and 9 are followed. 9 is bent: the walk is the SDK's own over .NET's `Validator`, because
  `Microsoft.Extensions.Validation`'s host API is experimental in .NET 10.0.9 (ASP0029, measured). The design says how
  the swap comes.
