- **2026-11-12 · A script that creates an issue inside itself is refused like the command it hides**:
  the session hook from #1430 read only the command line, so a local tool that called `gh issue create`
  from a Python `subprocess` list got past it, and #1475 and #1477 were born without points. The hook
  now reads the file of a script the command runs, and the scripts that file runs one level down, and
  refuses one that creates an issue, as a line, as an argument list or through the GraphQL
  `createIssue`. `scripts/new-issue.py` stays the one door, and a session that runs the old tool is
  now told to use it (#1478).
