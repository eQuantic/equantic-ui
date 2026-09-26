# Design

## One section, two files, one test

`CLAUDE.md` is what Claude Code reads; `AGENTS.md` is what the other agents and OpenSpec's
`agents` skills read. The working agreement has to be in both, and two hand-kept copies drift: the
current `AGENTS.md` is the proof, a fork of an older `CLAUDE.md` that still documents a removed
Tailwind integration. An `@AGENTS.md` import from `CLAUDE.md` would keep one copy, but it pulls the
whole file into every Claude session and leaves `CLAUDE.md` unreadable on its own. So the section is
written twice and compared by a test in `eQuantic.UI.Web.Tests`, beside the other documentation
guards: the instrument that fails, not the discipline that hopes. The rest of `AGENTS.md` becomes a
pointer to `CLAUDE.md` instead of a second, older guide.

## The hook acts where the harness cannot be trusted to

A laptop is set up by its owner, and a hook that rewrote a developer's git identity or installed an
SDK on every start would be an intrusion. So everything but the OpenSpec CLI runs only when
`CLAUDE_CODE_REMOTE` is `true`, which Claude Code sets in cloud sessions. The identity goes into the
repository's own git config and into the session's environment (`GIT_AUTHOR_*`, `GIT_COMMITTER_*`,
through `CLAUDE_ENV_FILE`), because an identity in the environment outranks every config file.
Signing is turned off in the repository's config, which outranks the container's global one.

## Pins

The .NET SDK archive is pinned by version and SHA-256 per architecture. The digests were taken from
archives whose SHA-512 matched Microsoft's release metadata, so the pin names the genuine file.
npm packages are pinned by the lockfile, whose integrity hashes `npm ci` verifies. Docker is part of
the cloud image and is only started, never downloaded.

The OpenSpec CLI runs on Node, the one piece of tooling here that does: building and testing the
SDK still needs nothing but the .NET SDK, and CI already uses Node for the VS Code extension. Running
it through the embedded bun would keep contributors off Node, at the price of a second mechanism
for CI and the cloud, which have Node anyway; one mechanism wins.

## Why CI runs the hook

The hook's cloud path is one no laptop takes, so nothing would notice the day it broke. The
`session-start` job runs it on a clean Linux runner the way a fresh container would, with a hostile
global git config and no `dotnet`, and asserts every promise. Two controls keep that check honest:
a commit before the hook must fail, and a copy of the hook with a wrong digest must refuse the SDK.
