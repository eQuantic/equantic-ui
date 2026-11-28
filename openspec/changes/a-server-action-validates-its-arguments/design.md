## Context

#1984, a Feature under the epic #898 (milestone 1.0.0). It was found while designing the masked field (#1966) and
is general: a Server Action's arguments are deserialized and handed to the method without their DataAnnotations
being checked (`ServerActionsMiddleware.cs`), so an app validates by hand, or not at all.

What the tree has:

- A refused call today is a status code with `{ success: false, error }` (`WriteErrorResponse`), which the client's
  `server-actions.ts` throws as a plain `Error` carrying the message.
- A Server Action runs in the culture of its page (`server-actions` spec).
- `FormController.SubmitAsync` turns a throw into `SubmitError`, and `ApplyServerErrors(IReadOnlyList<FieldError>)`
  puts a server's verdict on the fields, but nothing calls it for the developer. `FieldError` and `FormController`
  are `[TwinIsTranspiled]`.
- `[FormModel]` already turns a model's DataAnnotations into the browser's rules (`FormModelGenerator`), so the
  server's check is the one that counts and the browser's is the early one.

## Goals / Non-Goals

**Goals:** every argument's DataAnnotations checked before the action runs, nested models and collections
included; a refusal in .NET's own shape; the page's form showing it on its fields; the developer writing nothing.

**Non-Goals:** an opt-out (the owner's decision 4); FluentValidation or any validator that is not DataAnnotations;
validating what an action returns.

## How .NET and Flutter solve it

Flutter has no server. .NET has three answers: MVC's model binding validates into `ModelState` and
`[ApiController]` answers `400` with `ValidationProblemDetails`; minimal APIs in .NET 10 validate with
`builder.Services.AddValidation()`, backed by `Microsoft.Extensions.Validation` and its source generator, and
answer `400` with `HttpValidationProblemDetails`. The design takes .NET's refusal shape and its error keys, and
measures whether it can take its mechanism.

## Decisions

### 1. The walk, and the refusal


**Measured on .NET 10.0.9** (`measure/validation/validation.cs`):

- `Validator.TryValidateObject(…, validateAllProperties: true)` validates one object's own properties. It does
  NOT descend into a nested model: `Address.PostalCode` went unreported.
- `Microsoft.Extensions.Validation`, which backs minimal APIs' `AddValidation()`, ships in the ASP.NET Core 10
  shared framework with its source generator. It reports the nested error under the path `Address.PostalCode`.
- But the API a host needs to validate a type outside a minimal-API endpoint is EXPERIMENTAL in 10.0.9.
  `ValidatableTypeAttribute`, `ValidationOptions.TryGetValidatableTypeInfo` and `ValidateContext` fail the build
  with ASP0029, "for evaluation purposes only and is subject to change or removal".
- And its generator discovers a type only from a minimal-API endpoint or from `[ValidatableType]`, which a
  developer would have to write on every model.

**Decision: the server's own walk over .NET's `Validator`, keyed as `Microsoft.Extensions.Validation` keys its
errors.**

- **Arguments:** the middleware validates each deserialized argument before it invokes the action.
  - A scalar parameter's attributes go through `Validator.TryValidateValue`.
  - A model goes through `Validator.TryValidateObject`, which also runs `IValidatableObject`. The walk then
    descends into each property that holds a model, and into each element of a collection.
- **Paths:** errors are named by their path from the argument (`Cpf`, `Address.PostalCode`, `Items[0].Name`). A
  scalar argument is named by its parameter.
- **Reflection** reads each type's properties once and caches them. The Server is not AOT-compatible (it sets no
  `IsAotCompatible`), and the walk declares `RequiresUnreferencedCode`, as `Validator` does.
- **When `Microsoft.Extensions.Validation`'s host API leaves evaluation**, the walk is replaced by it, with the
  same keys (the owner's decision 3).

**What the page hears:**

1. A refused action answers `400` with `application/problem+json`, .NET's own `HttpValidationProblemDetails`
   shape: `status`, `title` and `errors`, a map from path to messages.
2. `server-actions.ts` throws `ServerValidationException`: a `[TwinIsTranspiled]` class in
   `eQuantic.UI.Primitives.Forms`, deriving from `Exception`, with `Errors` as an `IReadOnlyList<FieldError>`.
   A C# `catch (ServerValidationException e)` around an action call catches it.
3. `FormController.SubmitAsync` catches it and calls `ApplyServerErrors(e.Errors)`, so a form submitted with
   `form.SubmitAsync(() => Save(model))` shows the server's messages on its fields with no line of the
   developer's. A path no field is named by goes into `SubmitError`.
4. Messages are written in the request's culture (DataAnnotations' resources, and an attribute's own, `[Mask]`'s from `SdkStrings` once it ships),
   which is the page's.


## Principles followed and bent

**Followed:** 1 (the developer writes nothing), 3 (DataAnnotations, `HttpValidationProblemDetails`, the request's
culture), 9 (the instrument that fails: an invalid call never runs).

**Bent:** 9's "the mechanism .NET already has": the walk is the SDK's own over .NET's `Validator`, because the
mechanism .NET 10 has for this, `Microsoft.Extensions.Validation`, has an experimental host API (ASP0029,
measured) and discovers a type only through an attribute the developer would write. The keys are its keys, so the
swap changes no page.

## Risks / Trade-offs

- **An action that wanted an invalid model** (a draft saved half filled) is refused. → It takes a type without the
  attributes; there is no opt-out (the owner's decision 4).
- **A cycle in a model graph.** → The walk keeps the objects it has visited and does not enter one twice.
- **Reflection cost per call.** → Each type's members are read once and cached.

## Migration Plan

A server action whose arguments carry DataAnnotations now refuses invalid ones with `400` before it runs.
Migration line: an action that validated by hand may drop that code; one that must accept an invalid model takes a
type without the attributes.

## Decisions the owner took

- The SDK walks .NET's `Validator` now, to be swapped for `Microsoft.Extensions.Validation` when its host API
  leaves evaluation (decision 3 of the masked field's second round).
- No opt-out (decision 4).
- The name `ServerValidationException` stands (decision 6).

## Open Questions

None.
