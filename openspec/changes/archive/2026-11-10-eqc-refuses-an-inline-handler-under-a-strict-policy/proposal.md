# Proposal

Closes #1269, a Task under #306, slice 5 of 6. It builds on #1267, which gives an app
`UseContentSecurityPolicy`.

## Why

`HtmlElement.OnClientClick` and `OnLoad` take JavaScript as text and write it as `onclick` and
`onload`, and `DynamicElement.CustomAttributes` writes any attribute, an `on…` one included. They
stay: `HtmlElement` transcribes the DOM on purpose, and the escape hatch is the product principle's
stated exception. But a strict policy refuses every inline handler in the browser, wherever its
markup was written, and a developer heard of it only in the console of a page whose button did
nothing.

## What Changes

- The SDK's generator reads `UseContentSecurityPolicy` out of the app's source and writes
  `[assembly: ContentSecurityPolicy]`, the way the native capabilities reach their build.
- eqc reads the attribute and refuses, with **EQ2115**, every `OnClientClick` and `OnLoad` of an
  `HtmlElement`, and every constant `CustomAttributes` key of a `DynamicElement` that starts with
  `on`, in every source the build compiles: the app's and its referenced projects', since a library
  never sees the app's `Program.cs`. The message names what to write instead, an `OnClick` or an
  entry of `CustomEvents`, which the runtime attaches as a listener.
- An app that did not turn the policy on builds as it did.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `content-security`: the build refuses an inline handler under the app's policy.

## Impact

`src/eQuantic.UI.Server` (`ContentSecurityPolicyAttribute`), `src/eQuantic.UI.Generators`
(`ContentSecurityPolicyGenerator`), `src/eQuantic.UI.Compiler` (`InlineHandlers`, `AppBuild`),
`docs/DIAGNOSTICS.md`, and the wiki's Diagnostics page in five languages.
