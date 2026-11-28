Pull request E, Feature #1984 under #898. It is independent of the masked field and may merge at any time. A section ends with the check that proves it against the real thing.

## 15. A Server Action validates its arguments before it runs (5 points, #1984)

- [ ] 15.1 `ServerActionArgumentValidator` in the Server: each parameter's attributes through
  `Validator.TryValidateValue`, each model through `Validator.TryValidateObject`, and the recursive walk over model
  properties and collection elements.
  - Each type's members are read once and cached. Errors are keyed by path (`Address.PostalCode`,
    `Items[0].Name`), and `RequiresUnreferencedCode` is declared.
  - Test: `ServerActionValidationTests`, with the spec's nested model, a scalar parameter, a collection, an
    `IValidatableObject`, and a cycle in the graph that does not loop.
- [ ] 15.2 `ServerActionsMiddleware` validates before invoking, and answers `400` `application/problem+json` in
  `HttpValidationProblemDetails`'s shape, with messages in the request's culture.
  - Test: `ServerActionsMiddlewareTests`, which shows the action is not invoked, the body, and the pt-BR messages.
- [ ] 15.3 Check against the real thing: the endpoint called with an HTTP client in the server tests' test host
  (`WebApplicationFactory`), with a valid and an invalid body.

## 16. A page hears the server's verdict, and a form shows it (3 points, #1984)

- [ ] 16.1 `ServerValidationException` (`[TwinIsTranspiled]`, `eQuantic.UI.Primitives.Forms`), with `Errors`.
  `server-actions.ts` throws it on a `400` validation problem, and a plain error otherwise, as today.
  - Test: `server-actions.spec.ts`, and a conformance case catching it in C#.
- [ ] 16.2 `FormController.SubmitAsync` catches it and calls `ApplyServerErrors`. A path that names no field goes to
  `SubmitError`.
  - Test: `FormControllerTests`, and the transpiled twin's spec.
- [ ] 16.3 The wiki's server actions page and the forms page, in the five languages, say that arguments are
  validated and how a form shows it. A ledger entry cites #1984. `Upgrading` carries the behaviour change.
  - Test: the wiki guards (`EQ_WIKI_DIR`), and `LedgerTests`.
- [ ] 16.4 Check against the real thing: the dashboard's form submitted in Chrome with an invalid postal code,
  showing the server's message on the field.
- [ ] 16.5 Archive the change with `openspec archive a-server-action-validates-its-arguments --yes` before its pull
  request merges.
  - Check: `./scripts/check-openspec.sh`.
