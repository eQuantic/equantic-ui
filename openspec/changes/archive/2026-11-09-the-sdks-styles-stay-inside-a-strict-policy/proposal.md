# Proposal

Closes #1268, a Task under #306, slice 4 of 6. It builds on #1267, which writes the policy.

## Why

#1267's policy writes no `style-src`, because the SDK's styles were not ready for one: the runtime
created style elements without the page's nonce, the shell wrote stylesheet links without it, and
the realizers write `style` attributes on every page. A policy with `style-src` would have refused
them, and without one, styles are the part of a page a policy does not cover, which also keeps
`default-src` out of reach.

## What Changes

- The policy writes `style-src 'nonce-…'` and `style-src-attr 'unsafe-inline'`.
- Every style element and stylesheet link carries the nonce:
  - the shell's style elements, already since #1266;
  - its stylesheet links (`AddStylesheet`, a component's `StylesheetAsset`);
  - the style elements the runtime creates (`eq-atomic` and `eq-faces` on a page the server did not
    render), through one helper, `createStyleElement`, which reads the page's nonce off the element
    that carries it.
- Style attributes are admitted by `style-src-attr`, decided by measure (the design). The runtime's
  one `setAttribute('style', …)` writes through the CSSOM instead.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `content-security`: the policy's style directives, and the nonce on stylesheet links and on the
  runtime's style elements.

## Impact

`src/eQuantic.UI.Server` (`ContentSecurityPolicy`, `HtmlTag`, `Assets/IAsset.cs`),
`src/eQuantic.UI.Runtime` (`utils/page-nonce.ts`, `style-atomizer.ts`, `face-declarations.ts`,
`lowering.ts`), and the wiki's ServerIntegration page.
