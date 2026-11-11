# app-services Specification

## Purpose
TBD - created by archiving change an-apps-own-service-reaches-the-browser. Update Purpose after archive.

## Requirements

### Requirement: An app's own service reaches the browser from its registration

A component that takes an app's own service through its constructor, as an interface, SHALL get it on
the server, on Photon and in the browser from the app's registration in `Program.cs`
(`AddSingleton`, `AddScoped`, `AddTransient` or their `TryAdd` forms, in the app's files or a project
it references). In the browser, every place code resolves the service SHALL build the registered class
as the container builds it, through its widest public constructor with each dependency resolved the
same way, and SHALL keep one instance for a singleton and for a scoped service. A registration SHALL
cross only where browser code resolves its service. A required service the browser cannot build SHALL
fail the build with EQ2116 naming what to write.

#### Scenario: A registered service hydrates

- **WHEN** `Program.cs` registers `AddSingleton<ILedger, Ledger>()` and a page's component takes
  `ILedger ledger` through its constructor
- **THEN** the page's module resolves `ILedger` by providing `new Ledger()` as a singleton, and two
  modules that resolve it share one instance

#### Scenario: A service nobody registered

- **WHEN** a component takes `ILedger ledger`, not nullable, and nothing registers `ILedger`
- **THEN** the build fails with EQ2116, saying that the component needs `ILedger` in the browser and
  naming `builder.Services.AddSingleton<ILedger, TImplementation>()`

#### Scenario: A factory only the server can run

- **WHEN** `ILedger` is registered only with a factory lambda
- **THEN** the build fails with EQ2116, telling the app to register the class itself

#### Scenario: A service only the server uses

- **WHEN** `Program.cs` registers a service that no browser code resolves
- **THEN** its implementation is not compiled for the browser
