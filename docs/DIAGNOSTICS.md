# Diagnostics

Every build error and warning eqc can print, with what it means and what to do about it. The
codes are stable: they are what you search for, and what a suppression would name.

A guard keeps this page honest — `DiagnosticsDocumentedTests` fails the build when a code is
reported from `src/` and has no row here, when a row names a code that no longer exists, or when
a code gains a new reporting site. The last one is not a style rule: **EQ2101 was once two
unrelated errors** (a resx translation mismatch and `System.IO` in a client component), each
pinned by its own green test, because the two never met in one compilation.

Diagnostics print in MSBuild-canonical form, so the IDE and `dotnet build` both link them:

```text
Pages/Counter.cs(14,9): error EQ2002: C# 'goto' cannot be transpiled to JavaScript …
```

## EQ0xxx — the build host

The `eqc` process itself, before or around a compilation.

| Code | Meaning | What to do |
|---|---|---|
| `EQ0000` | A compilation error arrived with no code of its own. | Read the message — the code is missing, not the diagnosis. |
| `EQ0001` | eqc crashed. | A bug in the compiler. The message carries the exception; please report it with the source that triggered it. |
| `EQ0002` | The MSBuild reference list is empty, so the semantic model would be built from an incomplete compilation and named arguments could be emitted in the wrong order. | Rebuild (`dotnet build`). If it persists, `CompileEQuanticUI` ran without `FindReferenceAssembliesForReferences`. |

## EQ1xxx — no translation exists yet

A gap in the compiler, not in your code. The construct is legal C# and could be translated; today
nothing does it. These are the codes that shrink as the compiler grows.

| Code | Meaning | What to do |
|---|---|---|
| `EQ1001` | A C# **expression** kind has no transpilation strategy. | Rewrite it in a transpilable form, or add a conversion strategy for the construct. |
| `EQ1002` | A C# **statement** kind has no transpilation strategy. | As above. |
| `EQ1003` | A C# **node** (neither expression nor statement) has no strategy. | As above. |
| `EQ1004` | A strategy **matched** the construct but this exact form has no emission — the translation would be silently wrong, so it stops. | Rewrite the form, or add the missing case to the strategy. |
| `EQ1005` | Two types share a twin filename — either the same name twice, or two names that differ only in case (one file on Windows and macOS). Their twins would be ONE file and the second would overwrite the first. | Rename one of the types. |
| `EQ1006` | *(warning)* One type is declared in more than one place and eqc emits one module per declaration, so the twin holds only the first declaration's members. | Combine them into a single declaration, or keep the members a component uses together in one. Harmless when the other halves are server-only, which is why it does not stop the build. |

## EQ2001–EQ2009 — the construct cannot cross

Not a gap: there is no browser equivalent, or a translation would have to be a guess. These do
not shrink with compiler work — they are the shape of the target.

| Code | Meaning | What to do |
|---|---|---|
| `EQ2001` | A C# construct with no runtime equivalent in the browser (typed-reference intrinsics: `__makeref`, `__reftype`, `__refvalue`, `stackalloc`, pointers). | Restructure without it. |
| `EQ2002` | `goto`. | Restructure with loops and conditionals, or a labelled `break`/`continue` (those DO translate). |
| `EQ2003` | `new T()` on a type parameter — generic arguments are erased at runtime in JavaScript, so the concrete type is unknown. | Pass a factory (`Func<T>`) or the constructed value in. |
| `EQ2004` | An extension method whose declaring class is not part of this compilation, so nothing emits it. | Use an instance member, or bring the declaring source into the compilation. |
| `EQ2005` | An infinite iterator. Iterators are MATERIALISED into an array, so the loop would run forever instead of yielding lazily. | Give the loop an end (a bound, a `yield break`), or take what you need inside the method. |
| `EQ2006` | The member does not bind in the semantic model, so any translation would be a guess. | Either the code does not compile, or eqc is missing references / generated sources. Never guessed — see [Compiler](https://github.com/equantic/equantic-ui/wiki/Compiler). |
| `EQ2007` | A collection expression `with(…)` argument beyond a capacity hint — a JS array or `Set` takes no constructor comparer. | Drop the argument, or build the collection explicitly. |
| `EQ2008` | Query syntax using `join`, `let`, a second `from`, or `into` — its C# translation runs through compiler-generated transparent identifiers. Also the fenced initializer forms. | Rewrite in method syntax, where every operator is supported. |
| `EQ2009` | A component is declared more than once in one file (partial declarations). eqc emits one module per declaration and cannot merge them. | Combine the members into a single declaration. |

## EQ2100–EQ2101 — resx templates

The localization contract, checked on the build machine rather than when a visitor arrives.

| Code | Meaning | What to do |
|---|---|---|
| `EQ2100` | About **this call**: the template must be a valid composite format whose specifiers are inside the supported subset, and the call must pass every hole it declares. | Fix the argument count, or drop the out-of-subset specifier (alignment such as `{0,10}` is outside v1). |
| `EQ2101` | About the **translations**: every culture's resx is held against the neutral one, which is the arity contract. | Fix the culture's template — a dropped or extra `{n}` fails the build, not the page. |

## EQ2102–EQ2107, EQ2112 — the client/server boundary

A client component reached for an API that only exists on a server. The bridge is `[ServerAction]`.

| Code | Meaning |
|---|---|
| `EQ2102` | Direct database access (`Microsoft.EntityFrameworkCore`, `System.Data`). |
| `EQ2103` | Networking (`System.Net.Http`, `System.Net.Sockets`). |
| `EQ2104` | OS threading and locking (`Thread`, `Monitor`, `Mutex`, `Semaphore` — not `Task`). |
| `EQ2105` | Spawning processes (`System.Diagnostics.Process`). |
| `EQ2106` | Native interop / P-Invoke (`System.Runtime.InteropServices`). |
| `EQ2107` | Runtime IL generation (`System.Reflection.Emit`). |
| `EQ2112` | File-system access (`System.IO`). |

`EQ2112` sits apart from its family on purpose: `System.IO` was reported as `EQ2101` until that
code turned out to belong to the resx check above, which the wiki already published. It is not a
numbering slip to tidy back.

## EQ2108–EQ2111 — one meaning per target, or none

The construct translates, but would READ differently on the server and in the browser. Rather
than pick for you, eqc asks.

| Code | Meaning | What to do |
|---|---|---|
| `EQ2108` | A `CultureInfo` that is neither `InvariantCulture` nor `CurrentCulture` — only those two cross. | Format with an explicit specifier (`ToString("N2")` follows the app's culture on both targets), or convert with the invariant culture. |
| `EQ2109` | `ToString(CultureInfo.CurrentCulture)` with no specifier — the general format is outside the tested `Intl` subset. | Name the format: `ToString("N2")`, `ToString("F1")`. |
| `EQ2110` | A fractional number converted with no culture at all: C# follows the request's culture (a comma, in `pt`) and JavaScript is always invariant. | Say which you mean. |
| `EQ2111` | `GetService` with no type argument to cross — the registry is keyed by the interface NAME, and there is nothing to key on. | Call the generic overload. |
| `EQ2113` | `ConfigureAwait` is dropped — there is one context to resume on — and dropping it would discard a NON-CONSTANT argument without evaluating it. | Pass a constant, or evaluate the expression into a local first. |

## EQ3001–EQ3005 — the Photon app generator

| Code | Meaning |
|---|---|
| `EQ3001` | A Photon app has no program. |
| `EQ3002` | A Photon app has more than one program. |
| `EQ3003` | A capability's reason must be a constant. |
| `EQ3004` | An entitlement's key must be a constant — it is signed into the app at build time, so a key built at run time never reaches the signature. |
| `EQ3005` | A bundle fact must be constant — the Info.plist is written at build time, so an argument built at run time never reaches the app's manifest. Either argument counts, and whatever the method takes: a string, a bool, an `AppCategory`. |

## EQ3101–EQ3105 — the source generators

The generated factory surface and form models.

| Code | Meaning |
|---|---|
| `EQ3101` | A component elects more than one factory constructor (`[UiFactory]`). |
| `EQ3102` | Two components share a name. |
| `EQ3103` | A form model property has a type no text box can hold. |
| `EQ3104` | A validation attribute has no rule to become. |
| `EQ3105` | A `[Compare]` names a property this form does not have. |
