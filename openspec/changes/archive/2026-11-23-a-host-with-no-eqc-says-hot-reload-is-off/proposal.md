# Proposal

Closes #1757, a Bug under #1587.

## Why

A host that serves eQuantic.UI pages without being an eQuantic.UI app opened its Development log with a failure.
eQuantic.Auth's API sample is such a host: an ordinary ASP.NET Core project serving the pages of a library it
references. Hot reload asked the host's MSBuild for the SDK's `_EQuanticCompilerArguments` target, which only a
project built with eQuantic.UI.Sdk has. MSBuild answered `error MSB4057` and "Build failed", and the server printed
it. Nothing remembered the answer: every save of the host's C# asked MSBuild again, printed it again, and sent the
page the development overlay's error.

## What Changes

- When MSBuild refuses the target, the server asks it, by an evaluation with no target, whether the project is
  built with eQuantic.UI.Sdk at all, reading the `EqcCliPath` the SDK names for every project it builds. An app
  pays nothing for it, since the question is asked only on a refusal.
- A project that is not says once that hot reload is off for this host, in the server's own words. MSBuild is asked
  no more. A save rebuilds nothing, writes nothing and sends the page nothing, and the host's sources are watched
  no more.
- A refusal from a project that IS built with the SDK is reported as MSBuild gave it, as before.

Finding the eQuantic.UI project whose pages a host serves and running its eqc is out of reach: the pages often come
from a package, whose sources are not the developer's to edit.

## Impact

- `src/eQuantic.UI.Server/HotReload/RunningCompiler.cs`, `HotReloadService.cs`, `IModuleCompiler.cs` and
  `CompileOutcome.cs`. No API changes (internal types), and an app writes nothing new.
