# Proposal

Closes #1375, a Bug under #1174 (a sub-issue of #157), found reviewing #1374 (#1366).

## Why

`MapUI` started hot reload and disposed it on `IHostApplicationLifetime.ApplicationStopping`, and disposing a host does not stop it. A host disposed without `StopAsync`, as an integration test disposes one, kept hot reload running: its watchers on the content root and `wwwroot`, its timers, and the running compiler, whose MSBuild and eqc have that root as their working directory. A save under the root still started a rebuild after the host was gone, and Windows refused to remove the folder (#1366, which #1374 fixed in that test alone).

## What Changes

- Hot reload ends with the host's services as well as when the app stops, whichever comes first: `AddUI` registers a `HotReloadLifetime` singleton that the container disposes with the host's services, and `MapUI` hands it the hot-reload service it starts. The service's `Dispose` runs once.
- Nothing a developer writes changes. An app's integration test that disposes a host with hot reload on no longer leaves its watchers and its compiler running.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `hot-reload`: hot reload ends with its host, stopped or not.
