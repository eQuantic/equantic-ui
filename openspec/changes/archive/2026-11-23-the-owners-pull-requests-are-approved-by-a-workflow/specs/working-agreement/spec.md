## ADDED Requirements

### Requirement: A contributor's pull request waits for the owner's approval, and the owner's is approved by a workflow

The `main` ruleset SHALL require one approving review. A workflow on `pull_request_target`, running as `main` holds it
and running no code of the pull request, SHALL approve a pull request whose author, event actor and every commit are the
owner's account and whose head is a branch of this repository; it SHALL NOT approve any other pull request, nor a push another
account makes to the owner's branch.

#### Scenario: The owner opens a pull request

- **WHEN** the owner's account, or a session working as it, opens a pull request from a branch of this repository, or
  pushes to one, or moves one out of draft
- **THEN** the workflow approves it, and the merge queue accepts it once its checks are green

#### Scenario: A contributor opens a pull request

- **WHEN** another account opens a pull request
- **THEN** the workflow does not approve it, and it waits for the owner's review before it can enter the queue

#### Scenario: A contributor pushes to the owner's branch

- **WHEN** another account pushes a commit to a branch of an open pull request the owner wrote
- **THEN** the workflow does not approve that push, and the ruleset dismisses the earlier approval
