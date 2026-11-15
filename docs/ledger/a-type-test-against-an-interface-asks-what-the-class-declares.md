- **2026-11-12 · A type test against an interface asks what the value's class declares**: a Bug under #1174
  ([#1464](https://github.com/eQuantic/equantic-ui/issues/1464)). eqc wrote a test against an interface as
  `x != null`, true for any value, so the element engine's `(state as IDisposable)?.Dispose()` called a
  `dispose` no state had. The twin of every class, record, struct and component the compiler writes declares
  the interfaces a test can ask for in `static $interfaces`, and the test reads it, as .NET answers off the type.
  A test the browser cannot answer, an interface with type arguments, one a string, a number or an array
  implements, or one of the visual vocabulary, is refused with EQ2015.
