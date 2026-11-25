# Tasks

## 1. The workflow

- [x] 1.1 `.github/workflows/approve-owner-pull-requests.yml` on `pull_request_target`, approving when the author and
  the event's actor are the owner's account, the head is this repository's and the pull request is not a draft.
- [x] 1.2 No checkout and no code of the pull request run; the token has `pull-requests: write` and nothing else.

## 2. The working agreement

- [x] 2.1 CLAUDE.md and AGENTS.md, Workflow, pull requests, step 4: the approving review the ruleset requires, and who
  gives it.

## 3. Proof

- [ ] 3.1 On the first owner pull request after this one lands: the workflow's approval appears and
  `enqueuePullRequest` accepts the pull request. (A `pull_request_target` workflow runs as `main` holds it, so it
  cannot run on the pull request that brings it.)
