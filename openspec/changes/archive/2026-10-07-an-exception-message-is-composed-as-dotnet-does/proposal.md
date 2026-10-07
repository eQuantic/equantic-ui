# Proposal

Closes #558, a sub-issue of #565 (the transpiler's fences hold on every path).

## Why

A framework exception's constructor arguments were read by position, so the one argument of `new ArgumentNullException(nameof(x))` was taken for the message, and a page that shows `e.Message` showed the parameter's name. The parameter's name never reached a message: `new ArgumentException("bad", "x").Message` was "bad" where .NET writes "bad (Parameter 'x')". A constructor given no message gave an empty one, where each .NET type has its own text, and `ParamName`, `ActualValue` and `InnerException` read nothing.

## What Changes

- **eqc binds a framework exception's arguments by their parameters.** The message is the argument the constructor binds to `message`, and a constructor without one has none, never its first argument. The parameter name, the actual value, the inner exception, a disposed object's name, a type initializer's type and an aggregate's inner exceptions travel to `$eq.exceptions.create` by name, a params array written out taking every argument from its own on. An app's own exception is read as it always was, since its constructor may hand any parameter to its base.
- **The text where no message, or a null one, was given is read from .NET itself.** eqc runs on the runtime the app runs on, so it builds the framework type with the constructor the call binds, every argument null or empty, reads its `Message` and hands it with the creation, after the message where the message may be null. A table of each type's text was never complete: a `TaskCanceledException` took its base's, and `new SystemException()` and `new SystemException(null)` write two different texts. The runtime keeps only `Exception.Message`'s own text, which names the type.
- **The runtime composes the message as .NET does.** It appends the parameter's name as ` (Parameter 'x')`, puts the actual value and the object's name each on a line of their own, ends an aggregate's message with each inner one's and words a type initializer's as .NET does, a null name as ''. An argument exception the runtime throws on .NET's behalf carries its `ParamName` too, and the aggregate a cancellation throws is built from its callbacks' exceptions. The exception carries `paramName`, `actualValue`, `innerException`, `objectName` and `typeName`, which its members read, and an aggregate's `innerException` is its first inner one.
- **A template hole is its part's index in any number of digits.** Eleven inner exceptions are eleven arguments, and the eleventh was left in the generated code as text.

For a developer using the SDK: `throw new ArgumentNullException(nameof(x))` shows "Value cannot be null. (Parameter 'x')", as .NET does, and `e.ParamName` and `e.InnerException` answer. Nothing an app writes changes. The public surface does not move: `ExceptionTypes.IsFramework` and `ExceptionCreationStrategy.ArgumentIndex` are internal.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-exceptions`: a framework exception's message is composed as .NET composes it.

## Impact

- eqc: `ExceptionCreationStrategy` (the message by its parameter), `ExceptionTypes.Construction` (the parts by name), `ExceptionDefaultMessage` (the text read from .NET), `JsExprWriter` (holes of any number of digits).
- The runtime: `utils/exceptions.ts` (`create` composes the message, its table of texts removed), `utils/cancellation.ts` (the aggregate through its parts).
- The transpiled components that throw with a parameter name are regenerated: their messages gain ` (Parameter 'x')`.
- Tests: `ExceptionMessageConformanceTests` and `CancellationConformanceTests`, both sides; `ExceptionConstructionTests` and `JsTemplateTests`; `exceptions.spec.ts`.
