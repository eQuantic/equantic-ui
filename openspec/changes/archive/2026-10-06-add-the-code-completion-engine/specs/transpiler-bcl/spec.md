# Spec Delta

## ADDED Requirements

### Requirement: The cancellation trio cancels as .NET does

`CancellationTokenSource`, `CancellationToken` and `CancellationTokenRegistration` SHALL cross as the
runtime's, which SHALL behave as .NET's: callbacks run once, the last registered first; one registered
after the cancellation runs at once, and its registration is the default one, whose token is `None`;
a method group of the three keeps its receiver, read once; the callbacks that throw are gathered into an
`AggregateException` after the others ran; `ThrowIfCancellationRequested` throws an
`OperationCanceledException`; a disposed source refuses to cancel and lets go of its callbacks, a
registration still unregistering once after it, and a linked one lets go of the tokens it follows;
and `CancellationToken.None`,
`default` and `new CancellationToken(false)` are one token that never cancels. A member of the three
the runtime does not carry SHALL be refused at the build (EQ2004), a constructor among them: the
runtime's source takes a delay or nothing, and a `TimeProvider` beside the delay is refused.

#### Scenario: Three callbacks

- **WHEN** browser-side code registers callbacks writing `a`, `b` and `c` on a token and cancels its
  source twice
- **THEN** the log reads `cba`, as in .NET

#### Scenario: A source's Cancel as a callback

- **WHEN** browser-side code registers `inner.Cancel` on another source's token and cancels that source
- **THEN** `inner` is cancelled, as in .NET

#### Scenario: A registration after the cancellation

- **WHEN** a callback is registered on a token already cancelled
- **THEN** it runs at once, and its registration's token is `CancellationToken.None`, which cannot be
  cancelled, as in .NET

#### Scenario: A cancelled token asked to throw

- **WHEN** a cancelled token's `ThrowIfCancellationRequested` is caught as an
  `OperationCanceledException`
- **THEN** its message is `The operation was canceled.`, as in .NET

#### Scenario: A member with no twin

- **WHEN** client code calls `TryReset` on a source, or reads a token's `WaitHandle`
- **THEN** the build fails with EQ2004
