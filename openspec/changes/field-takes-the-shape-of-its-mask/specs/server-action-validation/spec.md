## ADDED Requirements

### Requirement: A Server Action validates its arguments before it runs

The Server Action endpoint SHALL validate every deserialized argument with .NET's DataAnnotations before it
invokes the action: a parameter's own attributes, every attribute and `IValidatableObject` of a model, and the
same, recursively, for each property that holds a model and each element of a collection. An argument that fails
SHALL keep the action from running.

#### Scenario: A nested model

- **WHEN** an action taking a `Patient` with `[Required] Name`, `[Range(0, 150)] Age` and an `Address` with
  `[Mask("00000-000")] PostalCode` is called with an empty name, an age of 200 and the postal code `0131`, in pt-BR
- **THEN** the action does not run, and the refusal names `Name`, `Age` and `Address.PostalCode`

#### Scenario: A scalar parameter

- **WHEN** an action `Load([Range(1, 100)] int page)` is called with 0
- **THEN** the action does not run, and the refusal names `page`

#### Scenario: A valid call

- **WHEN** the same actions are called with valid arguments
- **THEN** they run, and nothing of the validation is written to the response

### Requirement: A refusal reaches the page as .NET's validation problem

A refused call SHALL be answered `400` with `application/problem+json` in the shape of .NET's
`HttpValidationProblemDetails` (`status`, `title`, and `errors`, a map from each failing path to its messages, in
the request's culture). In the browser the call SHALL throw a `ServerValidationException` whose `Errors` list each
path and message as a `FieldError`, catchable in C#.

#### Scenario: Caught in C#

- **WHEN** a page calls the nested-model action above inside `try { … } catch (ServerValidationException e)`
- **THEN** `e.Errors` holds the three paths with their messages, in the page's culture

### Requirement: A form shows the server's verdict on its fields

`FormController.SubmitAsync` SHALL catch a `ServerValidationException` thrown by the submission and apply its
errors to the fields they name, and put an error naming no field of the form into `SubmitError`, with no code of
the developer's.

#### Scenario: A form submitted to the server

- **WHEN** a form generated from `Patient` with `[FormModel]` is submitted through
  `form.SubmitAsync(() => Save(model))` and the server refuses the postal code
- **THEN** the field `Address.PostalCode`, or its form's `SubmitError` when no field has that name, shows the
  server's message, and `SubmitAsync` answers false
