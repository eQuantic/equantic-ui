# Proposal

Closes #664, a Bug under #565 (the transpiler's fences hold on every path, continued), found while
measuring #627.

## Why

Hot reload promises that the page keeps its state across a save. It did not, for any write-once page:
on the dashboard sample, `Count: 3` came back `Count: 0` after an edit, under `dotnet run` and under
`dotnet watch` alike. The boot captured the page's `_state` bag, which no write-once page has (its
state is the class fields its twin declares), and the door the captured state went back in by, the
server-data adoption, takes only what the hydration manifest lists.

## What Changes

- The reload captures the page's own fields, without the runtime's beside them, and hands them to the
  reloaded page before it builds, each rebuilt in the shape its initializer gives it.
- A field the edit removed is left behind, a field it added keeps its initializer, and a field JSON
  cannot carry is left alone while the others cross.

For a developer: save a component, and the page comes back with the edit and the state it had.

## Impact

- The runtime (`dev/hot-reload-state.ts`) and the boot (`Resources/boot.ts`). eqc, the Server and the
  shells are untouched, and no public or developer surface moves.
- Stacked on #666, which decides when a page listens for rebuilds.
