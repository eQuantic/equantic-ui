- **2026-10-14 · A page's boundary reads only what crosses**: the client boundary (EQ2102–EQ2107,
  EQ2112) was a validator that walked every invocation of a stateful page's file, so a Server Action
  or a `[ServerOnly]` member that read a file was refused while a stateless page was never read, and a
  construction, a read or a method group built and threw in the browser. It is asked now where the
  conversion writes a symbol, matched by namespace, once a site; a `[ServerOnly]` method reaches no
  twin whatever type declares it ([#816](https://github.com/eQuantic/equantic-ui/issues/816)). A type
  the build writes no module for, the app's own top-level `[ServerOnly]` class or an attribute among
  them, is refused with EQ2010 where client code names it, as a nested one was; measured on main, a
  page that called `Vault.Key()` built and threw `Vault is not defined`
  ([#823](https://github.com/eQuantic/equantic-ui/issues/823)). A call through a contract a
  `[ServerOnly]` method fulfils, the interface member it implements or the method it overrides, is
  refused with EQ2010 too wherever its receiver can be the type that keeps the method, read over the
  whole build; measured on the branch, `((IShape)new Circle()).Area()` built and threw a TypeError
  ([#816](https://github.com/eQuantic/equantic-ui/issues/816)). Review then found more ways client
  code reached a member or a type no twin holds, each now refused with EQ2010 where it is written: a
  bare method group, a `[ServerOnly]` operator or conversion applied (`Amount a = 7` wrote
  `Amount.fromInt(7)`), an extension block's `[ServerOnly]` member, which was also emitted, an instance
  member of a kept-out type read off a value, a `[ServerOnly]` `Deconstruct`, `GetEnumerator` or
  `Dispose` reached implicitly through deconstruction, `foreach`, `[.. xs]` or `using`, the `MoveNext`,
  `Current` or `Dispose` of the enumerator a spread reads, and a `[ServerOnly]` `GetEnumerator` a LINQ
  operator reaches; a closed generic contract (`Bag : IEnumerable<int>`) now scopes its refusal to
  matching type arguments, a variant one (`ISource<object>`) to what its variance admits, a string
  literal no longer counts as a type reference, and a method the app names `@nameof` is no longer
  skipped as the `nameof` operator ([#944](https://github.com/eQuantic/equantic-ui/pull/944)). Proposed and archived through OpenSpec
  (`openspec/specs/client-boundary`).
