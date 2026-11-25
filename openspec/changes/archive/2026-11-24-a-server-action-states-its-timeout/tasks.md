## 1. The attribute states it, the stub honours it

- [x] 1.1 `ServerActionAttribute.TimeoutSeconds`, declared in the branch's `PublicAPI.Unshipped`. Verify: the Primitives build, RS0016 clean
- [x] 1.2 eqc reads it as a constant and writes `{ timeoutMs }` only where an action states one. Verify: `ServerActionTimeoutTests` (a stated value, none, 0, a `const` beside a `Name` and a hydrated result)
- [x] 1.3 EQ2017 refuses a value below 0 or above 600, at the argument's line. Verify: `ServerActionTimeoutTests`, `DiagnosticsDocumentedTests` with the baseline and `docs/DIAGNOSTICS.md`

## 2. The runtime waits that long

- [x] 2.1 `invoke` abandons the call at the stated time and not before, and at 30 s when none is stated. Verify: `server-actions.spec.ts`

## 3. Against the real thing

- [x] 3.1 A real app's build. Verify: `samples/DefaultUIDashboard` with `FormScreen.Register` marked `TimeoutSeconds = 2 * 60` serves `invoke("eQuantic.Console.FormScreen/Register",[email],{timeoutMs:120000})`, the constant read through MSBuild's semantic model
- [x] 3.2 In a browser. Verify: the same action made to take 35 s and marked `TimeoutSeconds = 60` answers at /form after 35 s, 200, its verdict on the e-mail field and no console error, where the default would have abandoned it at 30 (neither change is kept in the sample)

## 4. Documentation

- [x] 4.1 The wiki's Server Actions page in all five languages, and `CLAUDE.md`'s Server Actions section
- [x] 4.2 One entry for the ledger citing #1881, as a file of its own under `docs/ledger/`
