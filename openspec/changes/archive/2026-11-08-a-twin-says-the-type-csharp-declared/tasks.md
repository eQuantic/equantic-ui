## 1. The return type

- [x] 1.1 `TsNamedType`, and a local function's arrow carries what it says. Verify: `ATwinSaysItsDeclaredTypeTests`, and a primitive, a generic, an async function and `void` leave it to TypeScript

## 2. The temporary

- [x] 2.1 `Temporaries` holds a slot's type and declares it. Verify: `AnObjectInitializersTemporary_IsTheTypeItHolds`

## 3. Against the real thing

- [x] 3.1 The transpiled components regenerated (`EQ_UPDATE_TRANSPILED=1`) and the runtime's `tsc` over them, with no error

## 4. Documentation

- [x] 4.1 One entry for the ledger citing #793 and #1106, as a file of its own under `docs/ledger/`
