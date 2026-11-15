- **2026-11-10 · An array is the array it is**: bugs under #849 ([#711](https://github.com/eQuantic/equantic-ui/issues/711),
  [#1159](https://github.com/eQuantic/equantic-ui/issues/1159)). A `T[]` behind `ICollection<T>` or `IList<T>` is marked at its conversion and
  refuses a change of its size as .NET's does. A method's `params` parameter on a twin the build writes is one array, so an array handed whole
  meets no argument cap and the callee holds the caller's array. A constructor's stays a rest parameter, which its overloads dispatch on (#1192).
