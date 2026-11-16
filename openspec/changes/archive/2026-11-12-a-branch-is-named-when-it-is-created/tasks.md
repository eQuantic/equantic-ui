# Tasks

## 1. The hook

- [x] 1.1 It reads a command line as its simple commands, with the shell's quoting, and follows `cd` and `-C`
- [x] 1.2 It refuses in `.claude/settings.json`, before a Bash command that starts with git
- [x] 1.3 Check: the self-test's cases, and the hook run in a repository made for it (exit 2 for `claude/x` there, 0 for an existing name, another repository and input it cannot read); with the exit code changed to 0 the self-test fails
- [x] 1.4 The self-test runs in CI's `roadmap` job

## 2. The script

- [x] 2.1 The issue types are read from the repository
- [x] 2.2 Check against the real thing: #1493 and #1498 were created by it from an account outside the organization, where the same command stopped at a TypeError before
