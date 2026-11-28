## Context

The last of the masked field's five changes (pull request D, #1979 to #1983, under #1966), and the one that closes
#1966. The shared design is `mask-engine-held-to-dotnet`'s `design.md`, and this change ships its decisions 4 (the
component), 7's component and server halves, 9, 11 and 12. It needs A (the engine, the vocabulary), B and C (the field
edits on both targets), and its server check rides on `a-server-action-validates-its-arguments` (#1984) when that has
merged.

## Goals / Non-Goals

**Goals:** `MaskedInput`, composed from `TextInput` with one `MaskFormatter` per instance; served and hydrated; the
typed value with `validatingType`; `[Mask]` over the real `MaskedTextProvider`, and the `[FormModel]` rule; what the
field still asks for heard on both targets (`SemanticNode.Description` on the four shells); the wiki and the
dashboard.

**Non-Goals:** the engine and its corpus (A), the editing on each target (B, C), validating Server Actions (E).

## Decisions

All are the shared design's, cited, with what this change adds:

- **The component's signature and values** (shared decision 4): `string value`, `Action<MaskedText> onChanged`, the
  kind derived from the mask, the placeholder, the buffer while the page's value fits, `InsertKeyMode`, `Obscure`,
  `validatingType`, the incomplete and invalid errors when left.
- **`ValidatingType`** (shared decision 7): `IMaskedValueParser` and `MaskedValueParser<TValue>` join the
  `eQuantic.UI.Masking` surface here, which is why this change MODIFIES A's requirement listing that surface. The
  first task measures that eqc carries `DateOnly.TryParse` into the delegate.
- **`[Mask]`** (shared decision 9): `MaskAttribute` joins the same surface, over .NET's `MaskedTextProvider`, with
  `ValidatingType`; `FormModelGenerator` emits the engine's rule.
- **Accessibility** (shared decision 11): `SdkStrings.MaskRemaining`, `MaskIncomplete` and `MaskValueInvalid`;
  `SemanticNode.Description` and each shell's mapping.
- **SSR and hydration** (shared decision 12): the C# engine on the server, the twin on the client, a field typed into
  before hydration fitted once (the owner took this).

## Risks / Trade-offs

The shared design's; and, from this change, eqc may not carry a BCL method group with an `out` parameter into a
custom generic delegate. Task 13.1 measures it first, and a failure is the compiler's to fix (principle 14).

## Migration Plan

None of its own: `TextEditingValue`'s move ships with A. The wiki's `Upgrading` page carries it in this change.

## Open Questions

None. The owner's decisions are recorded in the shared design.
