# Proposal

Closes #1804, a Bug under #1587 ("The transpiler's fences hold on every path, part five"), left by #1775.

## Why

`HydrationAssert.PageAsync` failed on every page whose module imports a shared chunk for its side effects alone,
15 of a real site's 18 pages: the boot reported `Cannot find module './AboutPage-02zmryfp.js'` and the browser
drew over the server's markup. The harness rewrites each import into the absolute path its plugin fetches from
the server, and matched a side-effect import only with a space after `import`. A minified bundle writes none
(`import"./chunk.js"`), so the chunk stayed relative, which bun cannot resolve among the modules it fetches.

Measured with the embedded bun and the bundler's own options: the bundler writes such an import when a page
reaches a component (a class that extends an imported base) only through another component that lands in a
chunk apart from it. `tests/HydrationSite` had no such graph, so the suite passed.

## What Changes

- The harness matches an import with or without a space after the keyword.
- `tests/HydrationSite` gains the graph: a ribbon two shelf pages reach only through a showcase, and a page that
  reaches the ribbon itself. A test asserts the shelf page's module holds the side-effect import, and that the
  page hydrates.

`eQuantic.UI.Testing`'s harness is the only part reached. The public surface and the developer surface do not
move.

## Capabilities

### Modified Capabilities

- `component-testing`: a page that imports a chunk for its side effects is hydrated like any other.

## Principles

The fixture asserts what it stands for (the import is in the module), so a bundler that stops writing it fails
the case rather than leaving it to prove nothing.
