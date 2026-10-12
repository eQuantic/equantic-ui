# Spec Delta

## ADDED Requirements

### Requirement: A commit message carries no tool's attribution

CI SHALL fail when a commit message of a pull request, or of a push to main, carries a tool's
attribution in a shape the guard names: a wrapper's tags, a generated-with or generated-by line
naming the tool, an assistant's co-authorship, the attribution comment a project's pull request
opens with, or a link into the tool's own pages, a session's or a conversation's. A named phrase
SHALL be refused wherever a line break falls in it. Prose that names the tool, and a balanced markup
snippet a message quotes, SHALL pass. The guard's self-test SHALL hold a fixture that isolates each
named shape, and CI SHALL run it on every push.

#### Scenario: A squash message that took a project's pull request body

- **WHEN** the squash message of a pull request opened from a project carries the body's attribution
  comment, its link to the conversation and its signature line
- **THEN** the `commit-messages` job fails on the push to main, naming each line

#### Scenario: A signature the squash composer wrapped

- **WHEN** a signature line's phrase is broken across two lines, with no link on either
- **THEN** the guard refuses it, reported as wrapped

#### Scenario: Prose that names the tool

- **WHEN** a commit message says the diff was reviewed in Claude Code, wrapped after the tool's first word, and quotes CLAUDE.md's first line
- **THEN** the guard passes it
