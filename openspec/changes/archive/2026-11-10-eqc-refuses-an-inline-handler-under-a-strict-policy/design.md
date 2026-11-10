# Design

## Where the refusal lives

The issue asked for the idiom the native capabilities use: the fluent call in `Program.cs`, read by
a source generator into an assembly attribute, read by the build. The refusal itself is eqc's, and
not the generator's, for one reason: a component with an inline handler can live in a class library
the app references, whose own compilation never sees the app's `Program.cs`. eqc compiles the app
together with every project it references from source, so it is the one place that sees both the
declaration and every use.

## What is read

Every source of the build, not only the code that crosses to the browser. The browser refuses an
inline handler wherever its markup was written, so a `[ServerOnly]` member that renders an
`onclick` on the server is refused in the browser as surely as a page's own, unlike the client
boundary's checks, which ask only of the code that crosses (#816).

- `OnClientClick` and `OnLoad`, by symbol: a property declared by `HtmlElement` or a type derived
  from it, where it is WRITTEN. A read (a component that forwards or inspects it) puts nothing in the
  markup and is left alone.
- `CustomAttributes` keys, when they are constants that start with `on`: an initializer's
  `["onclick"] = …` or `{ "onclick", … }`, whether the dictionary is assigned in an object initializer
  or through its element (`element.CustomAttributes = new() { … }`), an index
  `.CustomAttributes["onclick"]`, and an `.Add("onclick", …)`. A key computed at run time cannot be judged at build time and is left alone.
  Every HTML attribute that starts with `on` is an event handler, so the prefix refuses nothing else.

## Proof

Through the real `AppBuild`, an app on disk with the attribute and six handlers in the app and, in
the other case, in a referenced library: six EQ2115 errors, each at its line, none for a `data-`,
an `aria-` or a `role` attribute. Without the policy, none.

## Report-only still refuses at build time

The build cannot see the environment: report-only is read from `EQuantic:ContentSecurityPolicy` at
run time, and the same assembly serves every environment. An app that turned the policy on is
refused its inline handlers in every build, so the one it deploys with the policy enforced holds none.
