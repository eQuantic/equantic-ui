- **2026-11-11 · An app's own service reaches the browser from its registration in `Program.cs`**: a
  component that took an app service through its constructor rendered on the server and on Photon and
  threw at hydration, since the browser's container held only the SDK's capabilities and no API an app
  writes could register anything there. eqc now reads the app's `AddSingleton`, `AddScoped` and
  `AddTransient` registrations, its referenced projects' included, and every place browser code
  resolves such a service provides the registered class, built as the container builds it, so the
  first module to resolve it registers the one instance the others find. A registration crosses only
  where the browser resolves its service, and one the browser cannot build is EQ2116 at the build. The
  message a missing service throws names that API instead of a host that had none
  ([#1335](https://github.com/eQuantic/equantic-ui/issues/1335)).
