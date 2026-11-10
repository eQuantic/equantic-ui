## Why

Closes #711 and #1159, sub-issues of Feature #849.

- A `T[]` behind `ICollection<T>` or `IList<T>` grew and shrank where .NET refuses the call, since a list and an array are one JavaScript array here.
- A method's `params` parameter was a rest parameter, so an array handed whole was spread into the call, past an engine's argument cap (about 638,000 in Bun, about 124,000 in V8), and the callee got a copy where .NET hands it the caller's array.

## What Changes

- The conversion from `T[]` to `ICollection<T>` or `IList<T>` marks the array (`$eq.collections.fixedSize`), where the static type still says what it is, and `Add`, `Clear`, `Remove`, `Insert` and `RemoveAt` through the face refuse it in .NET's words. `ICollection<T>.IsReadOnly` reads what the face holds.
- A method's, a local function's and a delegate's `params` parameter on a twin this build writes is the array it is: a call that lists its elements packs them, and an array handed whole is passed as it is.

## Limits

- A constructor's `params` parameter stays a rest parameter, which its overloads dispatch on: #1192.
- A parameter of the SDK's own stays one too, which a hand-written twin may spell.

## Impact

- Reaches the compiler (`ValueFlow`, a new `CollectionIsReadOnlyStrategy`, `BoundArguments`, `InvocationStrategy`, `MethodLowering`, `TypeScriptEmitter`, `LocalFunctionStatementStrategy`) and the runtime (`utils/view.ts`, `utils/collections.ts`, `utils/list.ts`).
- `PublicAPI.Unshipped.txt` adds the strategy and two `Eq` constants, on the compiler's tool surface.
