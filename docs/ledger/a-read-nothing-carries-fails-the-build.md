- **2026-10-14 · A .NET read nothing in the browser carries fails the build**: a property or a field
  of a .NET type that no strategy translated went out as its camel-cased name with no diagnostic, so
  `CultureInfo.CurrentCulture.Name` threw a ReferenceError and `Environment.NewLine`,
  `typeof(T).Name` or a list's `Capacity` read undefined, while a call of the same kind of member was
  already EQ2004. A read now fails the build with EQ2004 naming the member, once per chain and through
  `?.` too, unless the browser carries it by its name: a string's or an array's length, an
  exception's twin, a dictionary's pair, an interface's member. Every read of the kind the suites
  make was logged first, which found a char's casing converting its culture as a value. The BCL
  audit's 17 reads that were `native` and named nothing are fenced; a property pattern is still not
  asked (#920) ([#847](https://github.com/eQuantic/equantic-ui/issues/847)). Proposed and archived
  through OpenSpec (`openspec/specs/transpiler-bcl`).
