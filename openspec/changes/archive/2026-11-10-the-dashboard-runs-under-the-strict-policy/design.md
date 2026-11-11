# Design

## How Flutter answers it

Flutter's web build takes `--csp`, which turns off the code it would otherwise generate at run time,
and leaves it to the app to find out whether its pages run under a policy. It proves nothing in a
browser. The SDK writes the policy itself, so it owes the proof that what it writes admits what it
serves.

## The fixture asks the browser

The browser is the only party that enforces a policy, so the proof is a browser loading the pages.
The repository already has one, the Playwright suite under `tests/e2e`, which starts the dashboard
sample in `Production` (no `dotnet watch`, so the policy is enforced, not report-only).

- **The listener is in place before the page's first byte.** `addInitScript` registers a capture
  listener for `securitypolicyviolation` on the document, so a refusal while the shell is parsed
  is heard as well as one after hydration. Each violation is kept as the directive, the blocked
  source (or `inline`) and where it happened.
- **It fails on what it found, not on a count.** The test compares the list of violations with an
  empty one, so a failure prints every refusal of every page in one run.
- **The header is asserted too.** A page served with no policy, or with the report-only header,
  raises no violation, and would pass a fixture that only listened. Each page's
  `content-security-policy` header must carry `'strict-dynamic'`.
- **The pages come from the sample.** The routes are the links of the sample's home page, so a page
  added to the sample is covered without touching the fixture.
- **Both ways in.** A page loaded by address runs the shell and its module; a page reached by a
  click runs the runtime's navigation, which fetches and imports a module into a page that is
  already running. Each is a separate test.
- **`load`, never `networkidle`.** The live screen keeps a server-events stream open, so the network
  is never idle; the fixture waits for the page's load and then for half a second of quiet.

## The A/B

In a disposable worktree, one inline script with no nonce was put into the shell's template. The
fixture failed on every route, naming `script-src-elem refused inline`, and passes without it. The
sabotage was never committed.

## What CI does not run yet

`tests/e2e` is not part of CI (#286). The fixture is the instrument that fails; wiring the suite into
CI is that issue's.
