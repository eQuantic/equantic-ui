## ADDED Requirements

### Requirement: The lockfile Dependabot watches is the tree that runs

Every tree the pipeline installs SHALL be resolved from a lockfile that GitHub's dependency graph
reads, or from a lockfile that a check compares, package for package and version for version, with
one the graph reads. The runtime's `package-lock.json` SHALL resolve the same `name@version` set as
its `bun.lock`, both SHALL record the ranges of its `package.json`, and the tests SHALL fail naming
what differs when they do not. The extension's Marketplace publish SHALL run the vsce that its
`package-lock.json` pinned, installed with `npm ci`, and SHALL NOT resolve a tree of its own at
publish time.

#### Scenario: A fix updates the runtime's bun.lock and not its mirror

- **WHEN** a change moves `vitest` in `src/eQuantic.UI.Runtime/bun.lock` and leaves
  `package-lock.json` as it was
- **THEN** the Web tests fail, naming the `vitest` version found only in each lockfile and the
  command that resolves the other

#### Scenario: A range moves in the runtime's manifest alone

- **WHEN** a change moves the `vitest` range in `src/eQuantic.UI.Runtime/package.json` and neither
  lockfile is resolved again
- **THEN** the Web tests fail, saying the lockfiles were not resolved from the manifest as it is

#### Scenario: The extension is published

- **WHEN** an `ext-v*` tag runs `publish-extension`
- **THEN** it installs the extension's `package-lock.json` with `npm ci` and publishes the verified
  archive with the vsce that install put in `node_modules/.bin`
