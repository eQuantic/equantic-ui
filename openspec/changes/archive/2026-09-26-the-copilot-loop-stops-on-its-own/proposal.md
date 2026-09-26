# Proposal

Closes #446 and #287, two Tasks under #160 (Instruments that fail, not warn).

## Why

The working agreement asked for Copilot's review to be requested again until a round brought nothing
new. Measured on six recent pull requests (#411, #354, #418, #421, #431, #409), Copilot's light
review brought 69 findings in 47 rounds, about 1.5 a round, so that rule costs about one round per
finding: 14 rounds for #354, and 10 and counting for #418. The first three rounds carried 48% of the
findings, and many later ones were real defects, so a ceiling alone would lose defects unless
something else finds them first.

## What Changes

- **The author reviews the whole diff before opening the pull request** (in Claude Code,
  `/code-review high`), since Copilot's first round starts when the pull request opens.
- **Every finding is sorted.** A defect (the changed code does the wrong thing, or a guard passes
  where it should fail) is fixed, proved both ways, and earns another round. Hardening,
  documentation or a nit is fixed in the same push or filed as an issue, and earns none.
- **The loop ends at the first round with no defect, and after the third in any case.** A defect
  found later is still fixed and proved, without another round.
- **A round is asked for on purpose**, once per push of fixes. The repository's ruleset no longer
  reviews on push (`review_on_push` off since 2026-09-26), so a merge from main or a change to the
  body alone costs no round.
- **A pull request merges on its own CI, against the current main.** The ruleset requires thirteen
  of the CI's jobs and a head up to date with main (#287), applied on 2026-09-26 after two pull
  requests that each passed alone broke main together (#453).

For a developer using the SDK nothing changes. The parts reached are the Workflow section of
`CLAUDE.md` and `AGENTS.md`, `CONTRIBUTING.md` and the pull request template.
