## Why

Closes #552, #742, #768 and #954, sub-issues of Feature #849.

The browser holds an enum member as its camelCase key (a flags enum as its number), a float as a number, a `List<T>`, a `T[]` and a value tuple as one array, and an anonymous object and a value held as data (`Color`, `Curve`) as one plain object. Once C# boxed them into `object`, the runtime answered by what they turned out to be:

- A boxed member was a string of its name: `Enum.IsDefined(typeof(Status), (object)"pending")` was true, `(Status)(object)"pending"` succeeded, `Scale.Small.Equals((object)"small")` was true, and a member of another enum spelled the same was that member (#552, #954).
- A boxed float and a boxed member printed as the browser holds them: `object o = 0.1f; o.ToString()` was `0.10000000149011612`, and `object o = Tone.Warm` printed `warm` (#768).
- Two lists or two arrays held as `object` compared and hashed by their items, as two tuples must (#954).
- `Color.White.Equals((object)new { R = 255, G = 255, B = 255, A = 255 })` was true (#742).

Edgar chose the box and the mark (2026-10-31) over documenting the four as limits.

## What Changes

- A boxing conversion of an enum member or a float into `object` or `ValueType`, which `ValueFlow` sees at every site, boxes the value with its .NET type (`$eq.box`, `utils/box.ts`). The box answers `Equals`, `GetHashCode` and `ToString` as .NET's boxed value does, the formatter writes it in the culture in force, and JSON writes the value it holds, so the wire does not move. `Enum` and the interfaces keep the value as it is, since their calls (`HasFlag`, `CompareTo`) are the value's own lowerings.
- Every unboxing asks the box: a cast to an enum or to a primitive value type, a type pattern and a constant pattern over `object`, a `switch` over `object`, a declaration pattern's binding, `Convert` of an `object`, and `Enum.IsDefined` and `Enum.GetName` of an `object`. The overloads of `Enum.Parse`, `TryParse` and `GetValues` that take a `Type` answer boxed members. The receiver of an enum's or a float's `Equals(object)` is boxed as C# boxes it for the call.
- A list and an array converted to `object` are marked as compared by reference (`utils/marks.ts`), which `equals`, `sameKey` and the hash read; a tuple stays unmarked and compares by its elements.
- An anonymous object is marked where it is made and where `with` copies it, and is never equal to a value held as data of the same members.

## Impact

- Reaches `eQuantic.UI.Compiler` (`Boxing`, `ValueFlow`, `PatternConverter`, the cast, `is`, `switch`, `Equals`, `Enum` and anonymous object strategies, `Eq`) and the runtime (`utils/box.ts`, `utils/marks.ts`, `enums.ts`, `equals.ts`, `hash.ts`, `key-equality.ts`, `format.ts`, `number-convert.ts`, `eq.ts`).
- `PublicAPI.Unshipped.txt` of `eQuantic.UI.Compiler` adds five constants of `Eq`, a tool surface. `developer-surface.baseline.txt` does not move.
- What `object` holds at run time changes for an enum member and a float: TypeScript written by hand that reads `typeof x` of a value of a `List<object>` or a `Dictionary<string, object>` meets a box. The wiki's Upgrading page says so.
- An int and a double, a char and a string, a long and a ulong held as `object` stay one kind each, and a value boxed in generic code (a `T` into `object`) or into an interface keeps the representation it had: limits the wiki states.
