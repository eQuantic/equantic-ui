- **2026-11-23 · The owner's pull requests are approved by a workflow, and a contributor's still wait for the
  owner**: since 2026-11-21 the `main` ruleset requires one approving review, so that a contributor's pull request
  waits for the owner, and every session works as the owner's account, which GitHub never lets approve its own pull
  request: the merge queue refused them all. `.github/workflows/approve-owner-pull-requests.yml` approves on
  `pull_request_target`, as `main` holds it and running nothing of the pull request, only when the author and the
  event's actor are the owner's account. The working agreement says who approves what
  ([#1847](https://github.com/eQuantic/equantic-ui/issues/1847)). Proposed and archived through OpenSpec
  (`openspec/specs/working-agreement`).
