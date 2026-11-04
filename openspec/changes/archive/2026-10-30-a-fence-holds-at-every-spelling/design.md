## Context

`[ServerOnly]` on a method has been asked of its symbol since #1140. The other arm of `StaysOnTheServer`, the
Server Action's body, and the parser's list of actions still read the attribute lists by name.

## How Flutter answers it

Flutter has no server boundary. The nearest rule is its analyzer, which asks the resolved element and never
the spelling; the same reason `SemanticHelper` exists here.

## Decisions

- **One reading of the attribute off a symbol.** `PlainClassModule.ServerActionMark` sits beside `IsServerOnly`.
  `ApplicationSyntaxReference` gives the attribute as written, so `Name =` is read through an alias and from
  the other half of a partial method, which the method's own lists do not hold.
- **#943 is refused at the declaration.** The text and equality writers are many, and the runtime calls these
  members with no C# site at all, in a collection's lookup for one, so a refusal where they meet the type would
  stay a list. The declaration is one place, and it is the only one that is always there.
- **That inverts what #1146 pinned for a record.** There the twin kept its synthesized text and hash, which is
  safe for a dictionary and still prints what .NET would not. #943 asks that a page never does. With the
  override refused, `TwinOwnMembers.Overrides` no longer needs to step around a server-only override.
- **#951 refuses every kind of type**, an interface and an enum too: the null check was the fallback for all.
  A package's exception keeps its test, which reads the .NET type names the error carries.
- **#953 gives the keyword the identifier's treatment.** On the left of a member a keyword names a type, as
  `String` does, and the fences answer for the member. No strategy is added: it would translate nothing.

## Risks / Trade-offs

- Code that built and misbehaved now fails its build. The diagnostic says what to do, and we are in preview.
