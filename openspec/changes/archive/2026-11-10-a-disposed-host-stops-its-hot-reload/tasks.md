# Tasks

## 1. Hot reload ends with its host

- [x] 1.1 A singleton the container disposes holds the hot-reload service, and the service's `Dispose` runs once. Verify: `AHostDisposedWithoutBeingStopped_RebuildsNothingAfterIt` starts a rebuild on main's code and none after, with the hot-reload tests and the whole Server suite

## 2. Documentation

- [x] 2.1 One entry for the ledger citing #1375, as a file of its own under `docs/ledger/`
