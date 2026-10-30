## Why

Closes #401 and #1015, both sub-issues of Feature #849.

Two conversions of a number to an integer answered something .NET never answers:

- `Convert.ToInt32` and its siblings of anything but text were `$eq.math.round`: no range (`Convert.ToByte(300)` was 300), no BigInt (`Convert.ToInt64(2.5) + 1L` threw a TypeError), a decimal handed back unrounded, a char or an object's text read as a number.
- An explicit cast of a `double` or a `float` to an integer was its whole part wrapped, where .NET 9 and later saturate on every platform: `(int)1e12` was -727379968 and `(ulong)-1.0` was 2^64-1.

## What Changes

- `Convert.To<integer>` goes by the overload C# bound, to the runtime's `utils/number-convert.ts`: an integer of another width with its range checked, a double or a float rounded half to even, a decimal by `decimal.Round`, a char as its code unit, a bool as 1 or 0, a date refused, each overflow in .NET's words, which differ by overload. A `long` target answers a BigInt.
- An `object`, a nullable and an enum boxed for the call are read at run time: a null is 0, a text as the type's Parse reads it in the culture the call names, a number as the type it holds. `Convert.ToDouble`, `ToSingle`, `ToChar` and `ToBoolean` of an `object` read it the same way, and `ToChar` of a text or an integer is checked.
- An explicit cast of a `double` or a `float` calls `$eq.num.saturate`, `saturateUnsigned` or `saturateLong`. Measured on .NET 10: an `int`, a `uint`, a `long` and a `ulong` saturate at their own ends, and `byte`, `sbyte`, `short`, `ushort` and `char` take the low bits of the saturated `int`, so `(byte)300.0` is 44, `(byte)-1.0` is 255 and `(byte)1e12` is 255. The issue asked for every width to saturate at its own ends: the measurement decides.
- A char boxed in an `object` is read as a text of one character, and an `int` boxed in an `object` cannot be told from a `double` of the same value. That is #552's limit, stated here and in the runtime's header.

## Impact

- Reaches `eQuantic.UI.Compiler` (`ConvertStrategy`, `ValueFlow`, `CastExpressionStrategy`, `Eq`) and the runtime (`utils/number-convert.ts`, `utils/overflow.ts`, `eq.ts`).
- A behaviour an app could meet: a `Convert` that used to pass an out-of-range value through now throws, as .NET does.
- `PublicAPI.Unshipped.txt` gains the compiler's `Eq` constants, a `tool` surface. `developer-surface.baseline.txt` does not move.
