## ADDED Requirements

### Requirement: Every module imports the runtime's helpers where its text names them

Every module the build writes, a component's, a class's, a static class's, a record's and a struct's, SHALL import the
runtime's `$eq` where its emitted text names it, whichever strategy wrote the name, and only there. Whether a strategy
registered the helper SHALL NOT decide the import.

#### Scenario: A static class whose one runtime call is an escape

- **WHEN** an app's static class declares `Pay(string order) => $"{Checkout}?order={Uri.EscapeDataString(order)}"`,
  and a component calls it
- **THEN** the class's module imports `$eq` from the runtime, and the call answers as .NET's does, with type
  annotations on and off; it wrote `$eq.uri.escapeData` with no import, and threw "$eq is not defined" the first time
  the method ran
