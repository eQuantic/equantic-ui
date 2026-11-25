# Proposal

Closes #1847, a Task under #160 (Instruments that fail, not warn): the owner's pull requests are approved by a
workflow, and a contributor's still wait for the owner.

## Why

- Since 2026-11-21 the `main` ruleset (21198869) requires one approving review by someone with write access, so that
  every pull request a contributor opens waits for the owner.
- Every session works as the owner's account, and GitHub never lets an author approve their own pull request. The
  owner's pull requests could no longer enter the merge queue: `enqueuePullRequest` answered "At least 1 approving
  review is required by reviewers with write access", and the only way left was a bypass merge, which skips the
  queue's combined run.
- A ruleset cannot condition a rule on a pull request's author, so the rule stays and the owner's approval comes from a
  workflow.

## What Changes

- `.github/workflows/approve-owner-pull-requests.yml` approves a pull request on `pull_request_target` when its author
  and the actor of the event are the owner's account, its head is a branch of this repository and it is not a draft. It
  runs as `main` holds it, checks out nothing and runs nothing of the pull request.
- The working agreement (CLAUDE.md and AGENTS.md, Workflow, pull requests, step 4) says who approves what.

## Impact

- `.github/workflows/approve-owner-pull-requests.yml`, `CLAUDE.md`, `AGENTS.md`.
- Two settings outside the tree, the owner's: Actions may create and approve pull requests, and the ruleset dismisses
  stale approvals when new commits are pushed.
