# Design

## Measured in a real browser (Chromium 152, 2026-11-09)

A page under `script-src 'nonce-n'; style-src 'nonce-n'`, each case styling one element:

| Case | Applied |
|---|---|
| A `<style nonce>` in the markup | yes |
| A `<style>` without the nonce in the markup | no (`style-src-elem`) |
| A `<style>` the script creates, without the nonce | no (`style-src-elem`), and it gets no sheet |
| A `<style>` the script creates, with `element.nonce` set | yes |
| Text appended to a nonced style element | yes |
| A constructed `CSSStyleSheet` in `adoptedStyleSheets` | yes |
| `element.style.color = …` and `element.style.cssText = …` | yes |
| A `style="…"` attribute in the markup | no (`style-src-attr`) |
| `setAttribute('style', …)` | no (`style-src-attr`) |
| A `<link rel="stylesheet">` without the nonce | no |
| A `<link rel="stylesheet" nonce>` | yes |

With `style-src-attr 'unsafe-inline'` added, the two attribute cases apply, and every element case
answers as before: an unstamped style element is still refused.

And on the dashboard sample, served by the SDK: every page writes `style` attributes in its
server-rendered markup, from 4 to 25 a page. They are `--eq-pressed-bg` on every pressable (its
theme's pressed fill), `--eq-loop-from` on a looping motion, and the code editor's caret and overlay
positions. Six sites in the C# realizer write them, and six in the runtime's lowering.

## Decision: the attribute directive admits style attributes

- **Style elements and stylesheet links: by nonce.** The shell stamps its own (#1266) and its
  stylesheet links now. The runtime stamps the two it creates before they enter the document, which
  is when the browser decides and when it gives the element a sheet or none.
- **Style attributes: `style-src-attr 'unsafe-inline'`.** A style attribute cannot carry a nonce. The
  values are per element and often per frame (a caret's position), so moving them into classes would
  write a class per position and change the hydration contract of both realizers, and writing them
  through the CSSOM is not possible in server-rendered markup, which is styled before any script
  runs. A style attribute cannot run script, and the directive admits attributes only: an injected
  `<style>` element is still refused.
  What removing it would cost, so it is not read as an omission: the six sites in each realizer
  would write classes (a class per caret position and per pressed fill) or wait for the CSSOM after
  hydration, and both realizers' hydration contract would change with them.
- **A library that creates its own style element is handed the nonce.** ApexCharts writes its CSS
  in a style element it creates unless its `chart.nonce` option is set, so its initializer sets it to
  the page's. Chart.js draws on a canvas and needs nothing.
- **The runtime's `setAttribute('style', …)` goes.** The one call (the vector container) writes
  through `style.cssText`, which no policy governs, so it does not depend on the attribute directive.

## Not here

- `default-src`: now that styles have their directive it can follow, and it belongs with the browser
  proof of #1270, which will show what else a page loads.
- Inline handlers of the escape hatch: #1269.
