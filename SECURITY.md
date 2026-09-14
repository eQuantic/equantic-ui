# Security

## Reporting a vulnerability

Please do not open a public issue for a security problem. Use GitHub's private vulnerability
reporting instead: **Security → Report a vulnerability** on
[github.com/eQuantic/equantic-ui](https://github.com/eQuantic/equantic-ui/security/advisories/new).
Only the maintainers see the report, and you will hear back there.

Include what you can — the package and version, how to reproduce it, and what you believe the
impact is. A working reproduction is worth more than a long description.

## What is in scope

- The NuGet packages published from this repository (`eQuantic.UI.*`), including the MSBuild SDKs,
  the embedded compiler and bundler, and the native shells.
- The server pieces an app runs in production: Server-Side Rendering, Server Actions (the
  `[ServerAction]` whitelist, `[Authorize]` enforcement, payload validation) and the endpoints the
  SDK maps under `/_equantic/`.
- The browser runtime (`runtime.js`) and the code the compiler emits for an app.

Out of scope: vulnerabilities in third-party dependencies that are not reachable through this
project, and issues in an app's own code that the SDK does not generate.

## Supported versions

The project is in preview and every release is a prerelease (`0.2.0-preview.N`). Fixes land in the
next preview; there are no patch releases for older previews. Keep `global.json` on the latest
preview — the [Upgrading](https://github.com/eQuantic/equantic-ui/wiki/Upgrading) page and each
release's notes say what moved.

## What to expect

A reply within a few days acknowledging the report, a fix in the next preview once it is confirmed,
and credit in the release notes if you want it.
