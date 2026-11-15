## MODIFIED Requirements

### Requirement: An issue is created whole

An issue SHALL be created with what the plan counts it by. Every issue needs a type. An epic needs its
Priority and nothing above it. Every other issue needs an open parent under an epic, and points, one
of 1, 2, 3, 5, 8 or 13, except a feature or a story created to be split, which carries none of its own. `scripts/new-issue.py` SHALL refuse, before any request, an issue that lacks
one of them, naming what it lacks. It SHALL create the issue with its type, parent, milestone,
assignee and priority, put it on the board with its points and status, read it back, and fail naming
whatever did not land. A session's `PreToolUse` hook SHALL refuse the other ways a session creates an
issue (`gh issue create`, the GraphQL `createIssue`, a REST `POST` to `/issues` and an MCP tool that
creates one), naming the script, and SHALL let a read, a comment, an edit and a phrase quoted in an
argument pass. It SHALL read the file of a script the command runs, and the scripts that file runs one
level down, and refuse the command when one of them creates an issue (`gh issue create` as a line or
as an argument list, or the GraphQL `createIssue`); `scripts/new-issue.py` and the hook itself SHALL
pass.

#### Scenario: A task with no points

- **WHEN** `scripts/new-issue.py --type Task --parent 160 --title … --body-file …` is run
- **THEN** it exits 2 naming the points, and no issue is created

#### Scenario: An epic with no priority

- **WHEN** an epic is to be created without `--priority`
- **THEN** the script exits 2 naming the priority

#### Scenario: A session runs gh issue create

- **WHEN** a session's Bash command is `gh issue create --title x`
- **THEN** the hook exits 2, the command does not run, and the session is told to use the script

#### Scenario: A commit message names the command

- **WHEN** a session commits with a message that says "gh issue create"
- **THEN** the hook lets the commit run

#### Scenario: A script creates an issue inside itself

- **WHEN** a session runs `python3 file-subissues.py 1128 task …`, whose file calls
  `["gh", "issue", "create", …]` from a `subprocess`
- **THEN** the hook exits 2 and the script does not run

#### Scenario: A script only reads issues

- **WHEN** a session runs a script whose file lists or comments on issues and creates none
- **THEN** the hook lets it run
