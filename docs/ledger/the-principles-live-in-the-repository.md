- **2026-11-11 · The principles that decide designs, and how a change is proved, are written in the
  repository, and Copilot reviews with them**: a contributor besides the owner joined, and the rules
  that decide designs here had been learned over months and kept in the owner's head and in the
  private memory of the owner's agent sessions: some in `CLAUDE.md`, others in passing in audits and
  plans, a few nowhere. `docs/PRINCIPLES.md` now states fourteen, each with the case it came from and
  the test that holds it where one does, and `docs/PROVING.md` thirteen lessons on proof, from the A/B
  both ways to .NET as the oracle. GitHub Copilot, whose review the ruleset requires on every pull
  request, had no instructions; `.github/copilot-instructions.md` and four path-specific files (the
  compiler, the web, the vocabulary and the native track) give it the same rules, each under the
  4,000 characters its review reads, which `CopilotInstructionsTests` now holds along with each area
  file's `applyTo`. `CLAUDE.md`, `CONTRIBUTING.md`, `docs/README.md` and the
  OpenSpec proposal and task rules point to them
  ([#1418](https://github.com/eQuantic/equantic-ui/issues/1418)).
