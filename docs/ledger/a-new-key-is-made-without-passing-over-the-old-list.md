- **2026-11-12 · A new key is made without a pass over the old list**: the element engine looked each keyed member up
  in the old members' table and, when the table did not hold its key or held it for a member of another type, passed
  over every old member for one it could never match, so a list whose keys, or whose members' types, all changed cost
  a pass per member. Replacing 10,000 keys cost 2,457 ms on .NET against 2.4 ms to build them, and 2,726 ms against
  1.3 ms on the transpiled engine. It now costs a few times the build, held by a ruler of the ratio on both engines,
  and a new key inserted at the front, or every key replaced, is pinned on both sides to keep no other key's state,
  as it already did
  ([#1505](https://github.com/eQuantic/equantic-ui/issues/1505)). Proposed and archived through OpenSpec
  (`openspec/specs/element-tree`).
