# Proposal

Closes #1366, a Bug under #1174 (a sub-issue of #157).

## Why

`HotReloadDecisionTests.TheModulesCache_FollowsTheDecision_AndTheCSharpMapsStayInDevelopment`, the case with hot reload on, failed now and then on `test (windows-latest, rest)` of pull requests that touch no hot-reload code (#1352, #1362, #1365), with an `IOException` removing its temporary content root. Since that check became required, the failure blocks every merge it lands on.

The case disposed its app without stopping it. Hot reload lives as long as the app and is disposed when the app stops (`ApplicationStopping`), and disposing a host that was never stopped does not stop it, so the watchers on the content root and the MSBuild the running compiler asks how to start eqc, whose working directory is that root, went on holding the folder. Windows refuses to remove a folder something holds; Linux and macOS remove it, which is why the same head passed there.

## What Changes

- The cases that start an app stop it before disposing it, through one `Running` the test's `StartAsync` returns, so hot reload is disposed, its watchers closed and its MSBuild stopped, before the folder is removed. No retry around the delete.
- Nothing a developer using the SDK writes or meets changes: the change is in a test, and neither the public surface nor the developer surface moves.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none: the change is in a test alone, and no requirement of the product moves, so the change sets `skip_specs`.)
