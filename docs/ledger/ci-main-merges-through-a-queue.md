- **2026-11-09 · CI is ready for main to merge through GitHub's merge queue**: with two fronts merging
  at once, every merge left every other green pull request behind, to merge `main` again and wait
  about thirty minutes of CI before its turn, about two merges an hour. `ci.yml` now runs on
  `merge_group`, every required job runs there and no publishing job does. The commit guard reads its
  range from the event (`--event`), so a group's squash commits are read before they reach `main`,
  where the workflow's old expression gave a group the last commit alone; and a group's docs guards
  read master with the wiki branch of each pull request in the group merged onto it. Both self-tests
  hold the merge group's shapes and fail on the old behaviour. The queue itself is turned on in ruleset
  21198869 ([#1308](https://github.com/eQuantic/equantic-ui/issues/1308)). Proposed and archived
  through OpenSpec (`openspec/specs/working-agreement`).
