## Why

Closes #1855, a Bug under #1587, moved from the eqc-build queue.

A static generic class was written as a module the runtime's `tsc` refuses:

```csharp
public static class Cache<T>
{
    public static T? Last;
    public static T Keep(T value) { Last = value; return value; }
}
```

The twin declared `class Cache<T>`, every member of a static class is static, and TypeScript forbids a static member to
read its class's type parameters (TS2302, and TS6133 for the parameter nothing else read).

Beneath the types was a question of meaning. C# keeps one set of statics per closed type: `Cache<int>.Last` and
`Cache<string>.Last` are two fields, and each closed type runs its type initializer on its own first use. The twin is one
class for every type argument, so every closed type shared one set: the second closure read the first's state, and its
static constructor never ran. A class that only named the closed type to call it, `Cache<int>.Keep(1)`, did not even
import it (TS2552).

## What Changes

For a developer using the SDK nothing they write changes. A static generic class keeps its statics per closed type, as
.NET does.

- **The runtime keeps one object per closed type** (`$eq.statics(Cache, 'int')`, `utils/closures.ts`), made from the
  class with `Object.create` the first time it is used, with its own type initializer state. A static the class holds as
  a constant reads through to the class until the closed type writes its own.
- **The class's static members read their statics through `this`**, the closed type they were reached through
  (`ClosedStatics`): a static's getter and setter, the type initializer (`$init`, `$slots`), a static method, a call or a
  method group of a sibling, a lambda's body inside one.
- **Other code reaches the closed type it names.** `Cache<int>.Keep(1)` is `$eq.statics(Cache, 'int').keep(1)`, a method
  group is bound to it, and `using static` follows the same rule. The module imports the class.
- **The twin declares no type parameters**, and names those its members read with `any` aliases after the class, since
  one class serves every closed type.
- **A type argument known only at run time is refused** (EQ2018): `Cache<U>` in generic code, and `Cache<T>` from a type
  declared inside the class, name no closed type the browser can tell apart.

A generic class that is not static keeps its statics on the class: its instance members read them with no closed type in
hand. Parts reached: eqc (`ClosedStatics`, the identifier, invocation and generic-name strategies, the class module
writer) and the runtime's `closures.ts`. No contract surface or developer surface moves. `Eq.Statics` joins the
compiler's tool surface.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-classes`: a static generic class keeps one set of statics per closed type.

## Impact

- `StaticGenericClassConformanceTests` runs two closed types of one class on both sides, with type annotations and
  without. It fails on main.
- `SharedComponentTranspilationTests` gains `EmittedCache` and `EmittedCacheUser`, which the runtime's `tsc` refuses on
  main with TS2302, TS6133 and TS2552.
- `StaticGenericClassTests` holds the emission and EQ2018. `closures.spec.ts` holds the runtime's objects.
- EQ2018 has its row in `docs/DIAGNOSTICS.md` and in the wiki's Diagnostics page, in all five languages.
