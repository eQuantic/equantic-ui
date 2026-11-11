# Proposal

Closes #1335, a Bug under #1174, in the 1.0.0 milestone. Found building the Wallet's web head (#1334).

## Why

`WalletApp(IWalletLedger ledger, …)` takes an app service through its constructor. Photon resolves it
from the host's container, and the server renders the page in full. The browser then failed at
hydration with "WalletApp needs IWalletLedger, and this target has none. Register it with the host".
The browser's container held only the SDK's own capabilities, and no API an app writes could register
anything there, so the message pointed at nothing. So a component written once worked on two targets
and failed on the one 1.0 promises stable.

## What Changes

- The developer writes what they already write for the server: `builder.Services.AddSingleton<IWalletLedger,
  WalletLedger>()` in `Program.cs` (or `AddScoped`, `AddTransient`, or their `TryAdd` forms). The component
  gets the service in the browser too.
- eqc reads those registrations from the app's own files and its referenced projects. Every place
  browser code resolves the service (a component's constructor dependency, `GetService<T>()`,
  `CapabilityScope.Resolve<T>()`) carries
  `$eq.services.provide('IWalletLedger', () => new WalletLedger(), 'singleton')`. That registers the
  implementation the first time and answers the key, so whichever module runs first registers it, and
  every later one finds the same singleton.
- The implementation is built as the container builds it: its widest public constructor, with each
  dependency resolved the same way. Naming it in the output imports it, so it crosses as any type a page
  reaches does, and the client boundary judges it there.
- A registration crosses only where the browser resolves its service, so a server-only service is never
  compiled for the browser.
- **EQ2116**: a required service the browser cannot build fails the build. That covers a service that is
  not registered, registered only as a factory or an instance, or registered more than once, and an
  implementation with no public constructor, with a parameter no container provides, or that depends on
  itself.
- The message `CapabilityScope.Require` throws now names the API: `builder.Services.AddSingleton<…>()` in
  `Program.cs`.

Parts reached: eqc (`BrowserServices`, `ServiceRegistrations`, the service-provider strategy, the
emitter) and the runtime (`$eq.services.provide`). The public surface moves only in the compiler, a
`tool` assembly (`Eq.ProvideService`). The developer surface does not move, and nothing breaks.

## Capabilities

### New Capabilities

- `app-services`: an app's own service, registered in `Program.cs`, reaches every target.

### Modified Capabilities

(none)

## Impact

- `src/eQuantic.UI.Compiler`, `src/eQuantic.UI.Runtime/src/utils/services.ts` and
  `src/eQuantic.UI.Primitives/Contracts/CapabilityScope.cs` (the message).
- `docs/DIAGNOSTICS.md`.
- The wiki's ServerIntegration and Diagnostics pages, in five languages.
