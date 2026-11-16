## ADDED Requirements

### Requirement: A branch named outside the convention is refused when it is created

A session's command that creates a branch of this repository named outside `<type>/<slug>` SHALL be
refused before it runs, by a `PreToolUse` hook, with the convention as the reason. A name that
already exists in the repository or on origin, a command aimed at another repository, and a command
the hook cannot read SHALL pass. The hook's self-test SHALL run the hook in a repository made for
it, and CI SHALL run that self-test.

#### Scenario: A tool's own prefix

- **WHEN** a session runs `git switch -c claude/fix-the-thing` in this repository
- **THEN** the command is refused, and the session is told the types and the shape of the slug

#### Scenario: Reached through another directory

- **WHEN** a session in another directory runs `cd` into this repository and then `git checkout -b wip`, or names it with `git -C`
- **THEN** the command is refused

#### Scenario: A message that names the command

- **WHEN** a session commits with a message, quoted or in a here-document, that contains `git switch -c claude/x`
- **THEN** the commit passes

#### Scenario: A branch that is already someone's

- **WHEN** a session checks out a pull request's branch under its own name, or resets `main` from origin with `git checkout -B main origin/main`
- **THEN** the command passes: the name exists, and is not this session's to choose

#### Scenario: The wiki's clone

- **WHEN** a session runs `git -C ../equantic-ui.wiki switch -c anything`
- **THEN** the command passes

### Requirement: An account outside the organization can create an issue

`scripts/new-issue.py` SHALL read the issue types from the repository, which answers them to any
account that can open an issue there, and SHALL NOT depend on what the organization answers only to
its members.

#### Scenario: A contributor creates a task

- **WHEN** an account that is not a member of the organization runs the script with a type, a parent and points
- **THEN** the issue is created with its type, on the board, as it is for a member
