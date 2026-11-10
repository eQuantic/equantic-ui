# Proposal

Closes #1179, a Task under #306 (the security review).

## Why

Dependabot alert #87 (GHSA-xcx6-jx8p-x3h5, high: "MSAL Node authorization code flow may disclose
parameters to an attacker-controlled endpoint") reached `@azure/msal-node` 5.5.0 in
`extensions/vscode/package-lock.json`, through `@vscode/vsce` 3.9.2 and `@azure/identity` 4.13.1. The
only patched line is 7.1.0, and when #1179 was filed no released `@azure/identity` accepted it: 4.14.0
and 4.15.0-beta.1 both declared `@azure/msal-node: ^6.0.0`. The issue left the alert waiting on a
release rather than forcing one with an `overrides` entry, which would have put `@azure/identity` on a
major its own manifest refuses.

That release exists now. `@azure/identity` 4.14.1, published 2026-10-29 and npm's `latest`, says in
its changelog "Updated `@azure/msal-node` to `^7.1.0`"
([CHANGELOG, 4.14.1](https://github.com/Azure/azure-sdk-for-js/blob/main/sdk/identity/identity/CHANGELOG.md#4141-2026-10-29),
[Azure/azure-sdk-for-js#40532](https://github.com/Azure/azure-sdk-for-js/pull/40532)), and `@vscode/vsce`
3.9.2 accepts it, since it asks for `@azure/identity: ^4.1.0`.

## What Changes

The extension's lockfile resolves `@azure/identity` 4.14.1 and, through it, `@azure/msal-node` 7.2.1. It
moved by `npm update @azure/identity --package-lock-only`, so nothing outside that package's own tree
moved: `@azure/core-process`, `is-in-ssh` and `powershell-utils` are new, and `open` 10 → 11,
`wsl-utils` 0.1 → 1.0, `default-browser` and `bundle-name` patch releases follow it. No `overrides`.

For a developer using the SDK, nothing changes: the `.vsix` carries no `node_modules`, and these
packages serve `vsce` alone. The changelog's one break, interactive browser authentication requiring
`form_post` callbacks, is a flow `vsce` never uses: it authenticates through a chain of the
environment, Azure CLI, managed identity, PowerShell and Azure Developer CLI credentials.

The public surface and the developer surface do not move.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None: no requirement of the product moves, so the change sets `skip_specs: true`.

## Impact

- `extensions/vscode/package-lock.json`.
