# Proposal

Refers to #1473, a Bug under #1174, which it does not close.

## Why

The `samples` job takes 15 to 23 minutes on a merge group, and its edit-to-view step 2 to 4. Three
merge groups held a macOS runner 60 minutes instead (runs 43438536316, 43457527829 and 43767724402):
the edit-to-view step started and never ended, until the runner died with no log, and the queue
waited on each group before it could eject it. Neither the job nor the step had a limit of its own.

## What Changes

- The `samples` job stops at 60 minutes. GitHub's service enforces a job's limit, so it holds even
  where a starved runner cannot end its own step.
- Its edit-to-view step stops at 15 minutes, which leaves a desktop edit room to wait out its 300
  seconds twice and still report.

## What does not change

Why the desktop edit is sometimes never drawn on the runner stays #1473's question, and so does what
the check becomes (measured on the Reference backend, or no longer required). Those are the
maintainer's call. This bounds what a hang costs the queue and nothing else.

## Impact

- `.github/workflows/ci.yml`. No code, and an app writes nothing new.
