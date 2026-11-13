# Spec Delta

## ADDED Requirements

### Requirement: An issue a person took is left to them

An issue SHALL be taken by assigning it. Every issue starts assigned to the owner's account, which
every agent session works as; an issue assigned to anyone else SHALL be that person's, and so SHALL
one labelled `good first issue`, which is kept for a person's first step. No session SHALL start such
an issue or push to a branch of its. A contributor SHALL take an issue by assigning it to themselves
and moving it to In progress, and an issue already In progress SHALL be someone's, whoever it is
assigned to. `CONTRIBUTING.md` and the Workflow section of `CLAUDE.md` and `AGENTS.md` SHALL say so.

#### Scenario: A session looks for its next issue

- **WHEN** a session picks the next issue from the board, and an open issue in Backlog is assigned to
  a contributor rather than to the owner's account
- **THEN** the session passes over it, as it does an issue labelled `good first issue`

#### Scenario: A contributor takes an issue

- **WHEN** a contributor assigns an issue in Backlog to themselves and moves it to In progress
- **THEN** the issue is theirs, and no session starts it or pushes to a branch of its
