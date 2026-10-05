# Spec Delta

## ADDED Requirements

### Requirement: The cancellation trio cancels as .NET does

`CancellationTokenSource`, `CancellationToken` and `CancellationTokenRegistration` SHALL cross as the
runtime's, which SHALL behave as .NET's: callbacks run once, the last registered first; one registered
after the cancellation runs at once; the callbacks that throw are gathered into an
`AggregateException` after the others ran; `ThrowIfCancellationRequested` throws an
`OperationCanceledException`; a disposed source refuses to cancel; and `CancellationToken.None`,
`default` and `new CancellationToken(false)` are one token that never cancels. A member of the three
the runtime does not carry SHALL be refused at the build (EQ2004).

#### Scenario: Three callbacks

- **WHEN** browser-side code registers callbacks writing `a`, `b` and `c` on a token and cancels its
  source twice
- **THEN** the log reads `cba`, as in .NET

#### Scenario: A cancelled token asked to throw

- **WHEN** a cancelled token's `ThrowIfCancellationRequested` is caught as an
  `OperationCanceledException`
- **THEN** its message is `The operation was canceled.`, as in .NET

#### Scenario: A member with no twin

- **WHEN** client code calls `TryReset` on a source, or reads a token's `WaitHandle`
- **THEN** the build fails with EQ2004
