# Proposal

Closes #1177, a Task under #306 (the security hardening review).

## Why

Dependabot reports eleven open advisories against `src/eQuantic.UI.Runtime/package-lock.json`, three
high: `happy-dom` 20.10.2 (GHSA-834v-8h66-3h79), `vite` 6.4.3 (five, one high), `source-map-js`
1.2.1 (through vite and postcss, high), `@vitest/mocker` 4.1.11 (two, through vitest) and
`brace-expansion` 1.1.18 and 5.0.9 (one advisory, through the minimatch of eslint and of
typescript-eslint).

None of them reaches what the SDK ships. Each is a devDependency or arrives through one, and the
served `runtime.js` is written by the Server's `BundleRuntime` from `Resources/boot.ts`, which
imports no npm package. They run where the runtime is proven, `TestRuntime` on a developer's machine
and CI's `test-runtime` job.

## What Changes

- The manifest's floors rise to the patched versions: `happy-dom` `^21.0.4` (a major: 21.0.0 disables
  CSS and iframe loading by default, which no test relies on), `vite` `^6.4.4` and `vitest` `^4.1.12`.
- `bun.lock`, what `TestRuntime` installs from, records `happy-dom` 21.0.4, `vite` 6.4.5, `vitest` and
  its `@vitest/*` packages 4.1.12, `source-map-js` 1.2.2 and `brace-expansion` 1.1.21 and 5.0.12.
  Nothing else moves but the nested entries the embedded bun now hoists. A fresh resolution would
  move some sixty packages and give vitest a vite 8 of its own, so the lock moves by
  `bun install --lockfile-only` over the old one, and the two transitive packages, which bun's
  top-level overrides cannot reach in both majors of `brace-expansion`, by their version and
  integrity in place, their declared dependencies unchanged.
- `package-lock.json`, what Dependabot reads (#1180), records the same 233 versions.
- happy-dom 21 is the first with `Element.animate`, so the runtime's table glides now animate in the
  tests, and its `Animation.cancel()` rejects the finished promise without setting
  [[PromiseIsHandled]], which Web Animations requires (§ 4.4.14, step 1.3). A glide the runtime
  cancels mid-flight failed the run with three unhandled "The animation was canceled." rejections
  no browser raises. `src/test-environment.ts`, loaded by vitest as `setupFiles`, restores the
  standard by taking a handler on the promise before `cancel` rejects it, and
  `test-environment.spec.ts` holds both halves: nothing raised when nobody awaits it, an AbortError
  for a caller that does. The runtime itself does not change.
- Nothing a developer using the SDK writes or meets changes, and the served bundle is byte for byte
  the one before.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none: the change is in the runtime's test tooling alone, and no requirement of the product moves,
so the change sets `skip_specs`.)

## Impact

- **Runtime tooling**: `src/eQuantic.UI.Runtime/package.json`, `bun.lock`, `package-lock.json` and
  `vitest.config.ts`, and the test environment's `src/test-environment.ts` and its spec.
- **Bundle**: none. `runtime.js` and its map are byte-identical, so the served runtime's gzip budget
  does not move.
- **Public surface**: none. The developer surface does not move.
