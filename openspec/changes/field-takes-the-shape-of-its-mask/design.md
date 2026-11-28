## Context

#1966 asks for a field that takes the shape of a mask (a CPF, a phone number, a postal code, a plate, a
card's expiry), on the web and on Photon, written in .NET's own mask language. The owner settled three
things on 2026-11-28 and they are not reopened here: the component is `MaskedInput`; the language is
`System.ComponentModel.MaskedTextProvider`'s, with `MaskedTextBox`'s syntax; it enters 1.0.

The general capability under the request is wider than masks: **a field whose edits pass through a rule
before they land**, and which can therefore move the caret, refuse a character, or rewrite what was pasted.
The mask is the first rule; the vocabulary gains the hook, and the mask is one implementation of it.

What the tree has today, read before designing:

- `TextEntry` (vocabulary) carries a value and an `OnChanged(string)`. On the web it lowers to a real
  `<input>` and the BROWSER owns the caret, the selection and the IME; the runtime listens to `input` only
  (`lowering.ts`, `lowerTextEntry`). On Photon, `PhotonHost` owns editing: every edit reaches one of
  `TextInput(text)`, `EditKey` (Backspace, Delete, cut, paste), `SetMarkedText`/`CommitText` (a desktop
  composition) and `UpdateEditingValue` (a phone's keyboard, Flutter's whole `TextEditingValue`), and all of
  them end in `Commit(entry, value, caret)`.
- The input family: `TextInput` (the design system's field, composes a `TextEntry`), `NumberInput` and
  `MoneyInput` (#1764), stateful components that keep what is typed in a buffer while the page's value
  "says" the same thing (`MoneyInput.AdoptConfig`), and `TextEntryKind`, which says what a field takes and
  from which each target derives its keyboard.
- The write-once engines: `eQuantic.UI.Code` and `eQuantic.UI.Sheet`, C# transpiled into the runtime, driven
  by both realizers through a vocabulary interface (`ICodeSurfaceModel`, `ISheetSurfaceModel`), and held
  together by twin tests that run the C# under .NET and its twin in the served bundle under the embedded
  Bun (`tests/eQuantic.UI.Conformance.Tests/Code*TwinTests.cs`).
- Forms: `[FormModel]` turns a model's DataAnnotations into a `FormController` at build time
  (`FormModelGenerator`); server actions do not validate their arguments, and an app validates on the server
  with .NET's own `Validator.TryValidateObject`.

## Goals / Non-Goals

**Goals:**

- One mask engine, in C#, transpiled into the runtime, held to `MaskedTextProvider` and to `MaskedTextBox`'s
  editing by a corpus measured from .NET and pinned, executed on both sides.
- A vocabulary hook every host calls before an edit lands, so the engine is written once and the hosts only
  translate their events into one edit shape.
- `MaskedInput`, composed from `TextInput`, handing on the raw value, the formatted one and whether the mask
  is complete.
- Server validation with `MaskedTextProvider` itself, through a DataAnnotations attribute.
- The web (SSR and hydration included), and Photon on macOS, iOS, Android and Windows.
- Assistive technology hears what the field holds and what it still asks for, on both targets.

**Non-Goals (fenced out of 1.0, each an issue of its own if wanted):**

- Overwrite mode (`InsertKeyMode`, the Insert key). `MaskedTextBox`'s default is insert; overwrite is a
  second editing model and nobody asked for it.
- `PasswordChar` with visible literals. `MaskedInput` does not take `Obscure` in 1.0 (open question 7).
- `ValidatingType` (`MaskedTextBox` parsing the text into a `DateTime` or a number). A date is a
  `DatePicker`'s job, and an amount a `MoneyInput`'s.
- Undo. `MaskedTextBox` has none (`CanUndo` is `false`, `Undo()` is empty, winforms source at
  `3248f2c4`), and a browser's own undo stack does nothing once a script wrote the value (measured, below).
- Automatic DataAnnotations validation of server actions' arguments: a capability of the server actions,
  not of masks (candidate issue in the report).

## How Flutter solves it

The question is about Flutter's FIRST tree and the `State` behind it: a `TextField` widget takes
`inputFormatters`, a list of `TextInputFormatter`, and `EditableTextState` runs every edit the platform
reports (`TextInputClient.updateEditingValue`) through them before the controller takes it:
`TextEditingValue formatEditUpdate(TextEditingValue oldValue, TextEditingValue newValue)`. The
`mask_text_input_formatter` package is one such formatter. docs/FLUTTER-PARITY.md rows 118 and 119 record
that `TextEditingValue` and `TextInputSession` already follow Flutter on Photon; row 170 records
`TextEntryKind` against `TextInputType`.

Doing better than Flutter on one point: a formatter that compares two whole values must GUESS the edit
(was `11` → `1` the first or the second digit? a Backspace or a Delete?), and `mask_text_input_formatter`
guesses. Every host here already KNOWS the edit (the web's `beforeinput` names it, and `PhotonHost`'s
funnels are each one kind of edit), and `MaskedTextBox` decides differently for a Backspace and a Delete
that remove nothing (measured: the caret crosses the literal backwards for one and forwards for the other).
So the hook takes the edit, not a second value. Only a phone's keyboard hands a whole value, and the host
derives the edit from it once, as Flutter's `TextEditingDeltaInsertion`/`Deletion`/`Replacement` do.

## Decisions

### 1. The vocabulary: a formatter on `TextEntry`

`eQuantic.UI.Primitives` gains:

```csharp
public interface ITextInputFormatter
{
    /// The field's state after the edit, or `current` itself when the edit is refused.
    TextEditingValue Format(TextEditingValue current, TextEdit edit);

    /// Whether what the field takes is prose (spelling, autocorrection, a capital first letter).
    bool Prose { get; }
}

public readonly record struct TextEdit(int Start, int End, string Text, TextEditKind Kind);

public enum TextEditKind : byte { Typed = 0, Pasted = 1, DeletedBackward = 2, DeletedForward = 3 }
```

and `TextEntry.Formatter` (`ITextInputFormatter?`). `TextEditingValue` MOVES from
`eQuantic.UI.Native.Components` to Primitives, unchanged: both targets now hand it, and its names are
Flutter's. `TextInput` and `FormInput` take `formatter` as a constructor parameter and hand it to their
entry. A developer can write their own formatter (a field that upper-cases, a card number in groups of
four); `MaskedInput` is the SDK's one.

Why an interface on the node, and not a `Mask` property on `TextEntry`: a `Mask` on the vocabulary would make
both realizers know masks, and each would call the engine itself; the interface keeps them knowing ONE
protocol, as `ICodeSurfaceModel` does, and the mask stays in the library (principle 4, principle 15: a
capability composed, not a flag on one node). The names would exist without the web (principle 5):
"formatter", "edit", "typed", "pasted" are what a person does on every target.

`TextEditKind` is needed, and measured: a Backspace after the dot of `123.4__` and a Delete before it remove
the same position, but `MaskedTextBox` leaves the caret at 3 after one and at 4 after the other when nothing
is removed (corpus, "cpf backspace over a literal, digits after" and "cpf delete at a literal"). And `Typed`
and `Pasted` are two composites in `MaskedTextBox` (`OnKeyPress` → `PlaceChar` per character, and
`Replace(string, start, length)`), which differ when a character is refused.

### 2. The engine: in `eQuantic.UI.Components`, internal, held to .NET

Two competing designs were weighed.

- **A. A project of its own, `eQuantic.UI.Masking`,** as Code and Sheet are. Costs: a package, a
  `PackageReference` delivered by `Sdk.props`, PublicAPI files, a surface class, the packaging check, and
  its route into the runtime as `[RuntimeProvided]`. Gains: the engine is reachable without Components.
- **B. Inside `eQuantic.UI.Components`** (chosen). Every consumer of the engine already references
  Components: `MaskedInput`, the generated form rule, and an app writing its own `TextInput(formatter:)`. The
  engine is about 700 lines; Code and Sheet are their own projects because they are editors with their own
  documents, keymaps and histories, which a mask is not. The server's validation does not use the engine at
  all (decision 6), so nothing outside Components needs it.

The engine is `internal sealed class MaskEngine` (the parsed mask: a descriptor per position, the culture's
literals resolved at construction, as `MaskedTextProvider` resolves them) with the provider's operations by
.NET's own names (`InsertAt`, `Replace`, `RemoveAt`, `Set`, `Add`, `Remove`, `Clear`, `ToString(bool, bool)`,
`ToDisplayString`, `FindEditPositionFrom`, `FindAssignedEditPositionFrom`, `FindUnassignedEditPositionFrom`,
`IsEditPosition`, `VerifyEscapeChar`, `MaskCompleted`, `MaskFull`, `LastAssignedPosition`,
`MaskedTextResultHint`-shaped hints as an internal enum), and `MaskedTextBox`'s composites over them
(`Type`, `Delete(backward)`, `Paste`, `SetText`), transcribed from winforms `MaskedTextBox.cs` at
`3248f2c4b8839706952bb7d863a0b2b957b745f7` and cited there. Its public face is one type:

```csharp
public sealed class MaskFormatter(string mask, bool asciiOnly = false) : ITextInputFormatter
{
    public string Mask { get; }
    public bool AsciiOnly { get; }
    public TextEditingValue Format(TextEditingValue current, TextEdit edit);
    public bool Prose => false;
    public MaskedText Read(string text);   // the values a field's text holds
    public string Fit(string value);       // MaskedTextBox's Text setter: a value fitted to the mask
}

public readonly record struct MaskedText(string Value, string Text, bool MaskCompleted, bool MaskFull);
```

**The engine is stateless between edits:** `Format` rebuilds the state from the field's text, position by
position (the text a field draws is aligned to the mask, decision 4), applies the one edit, and writes the
text back. This is what lets every host stay as it is (the field's text IS the state) and what makes a page
that resets the value safe: there is no second copy to go stale. It is lossless because the SDK fixes
`ResetOnSpace` and `ResetOnPrompt` to `true` (.NET's defaults): a space or a prompt in the text reads back as
an empty position (measured: `set:1 2345678` on `(99) 9999-9999` gives `(1_) 2345-678_`, and
`set:1 3` on `00000-000` gives `1_3__-___`).

**What the engine reads of .NET, and the gap it meets.** `MaskedTextProvider` reads `.` as the culture's
`NumberDecimalSeparator`, `,` as `NumberGroupSeparator`, `$` as `CurrencySymbol`, `/` as
`DateTimeFormat.DateSeparator` and `:` as `TimeSeparator`, and classifies with `char.IsDigit`,
`char.IsLetter`, `char.IsLetterOrDigit` and the culture's `ToUpper`/`ToLower`. eqc carries the char methods
(`CharMethodStrategy`, Unicode classes) and `NumberFormatInfo`'s members (`PlatformMemberRead`), but NOT
`DateTimeFormatInfo.DateSeparator` and `TimeSeparator`, although the runtime's culture data has both
(`utils/culture.ts`). That is a gap of the SDK (principle 14), closed in this change as its own slice,
rather than worked around in the engine with a hardcoded `/`.

### 3. Holding the engine to .NET: a corpus measured, pinned, and executed twice

The truth is .NET, measured (PROVING §3), in two layers:

1. **`MaskedTextProvider`'s operations**, run on the real class: `Add`, `InsertAt`, `Replace` (char and
   string, at a position and over a range), `RemoveAt` (one and a range), `Remove`, `Set`, `Clear`, and after
   each the state (`ToString` with every flag, `ToDisplayString`, `MaskCompleted`, `MaskFull`, the counts,
   `LastAssignedPosition`), the result, the test position and the hint, plus every position's
   `IsEditPosition` and `Find…PositionFrom` in both directions.
2. **`MaskedTextBox`'s editing composites** (typing a character, Backspace, Delete, paste, setting the text),
   which are winforms code over the provider and cannot run here (WinForms is Windows-only): the test project
   carries them transcribed over the REAL `MaskedTextProvider`, cited at the winforms commit, so the oracle
   for every primitive under them is still .NET's.

The corpus is a JSON file pinned in the conformance project (`Masks/masked-text-provider.corpus.json`),
generated by a test from the real class. Without `EQ_UPDATE_MASK_CORPUS=1` the test regenerates it in memory
and FAILS on any difference (a .NET servicing change, a corpus case edited by hand); with it, it writes the
file. Three runs read it: the real `MaskedTextProvider` (the regeneration), the C# `MaskEngine` under .NET,
and the engine's twin in the served bundle under the embedded Bun, as `CodeLinesTwinTests` runs the code
engine. Beside the pinned cases, seeded random sessions (masks drawn from the corpus's, edits drawn from the
four kinds over random selections) compare the three live, so the corpus is not the only question asked.

The first corpus is measured already, from `measure/measure.cs` (68 cases, .NET 10.0.9), and its findings
decide this design:

- **`.` and `,` are the culture's separators, not literals.** `000.000.000-00` in pt-BR is drawn
  `___,___,___-__` and a CPF typed into it reads `123,456,789-01`; `Set("123.456.789-01")` fails at position 4
  with `DigitExpected`. A CPF is `000\.000\.000-00` in .NET's language. `$999,999.00` is `R$___.___,__` in
  pt-BR and `€___.___,__` in de-DE; `00/00/0000` is `__.__.____` in de-DE; `00:00` is `__.__` in fi-FI.
- **The caret skips a literal on the NEXT character, not after the last one:** after `1`, `2`, `3` in a CPF the
  caret stands at 3, before the separator, and `4` lands at 4 with the caret at 5. Typing the separator itself
  at 2 of `00/00/0000` steps over it (caret 3), because `SkipLiterals` is on.
- **A deletion keeps the literals in place and shifts what follows:** Backspace right after the separator
  with digits after it removes nothing and puts the caret before the separator; the next Backspace removes
  `3` and shifts the rest left (`123,456,789-01` → `124,567,890-1_`).
- **A paste is fitted character by character, and a refused character is skipped:** `123.456.789-01` pasted
  into a pt-BR CPF is refused twice (the dots) and still completes the mask; `+55 11 98765-4321` pasted into
  `(00) 00000-0000` gives `(55) _11_9-8765`, because a space is a reset (it empties a position).
- **Optional positions keep their gap:** in `(99) 9999-9999` the raw value of `(1_) 2345-678_` is
  `1 2345678`, with the space, and the mask is complete with no position assigned at all.
- **Any Unicode decimal digit is a digit**, `AsciiOnly` or not: `１` (fullwidth) and `١` (Arabic-Indic) fill
  a `0`; `AsciiOnly` refuses `é` and `ç` in an `L`.
- **`AllowPromptAsInput` only matters with `ResetOnPrompt` off**, and the SDK keeps `ResetOnPrompt` on.

### 4. What the field draws, and the values it hands on

The field draws `ToString(includePrompt: false, includeLiterals: true)` cut after the last assigned position:
the shape grows as it is typed (`1`, `12`, `123`, `123.4`), and an empty field is empty, so its placeholder
shows. Unassigned positions before the last assigned one are spaces, which keeps the text aligned to the
mask position by position (decision 2). The placeholder defaults to the mask's prompt shape
(`___.___.___-__`). This is the one place the design parts from `MaskedTextBox`'s look (which draws the
prompts always), for two measured reasons: a screen reader reads the drawn text as the value (winforms'
`MaskedTextBoxAccessibleObject.ValueInternal` is `WindowText`, prompts included), and the web cannot draw a
character that is not in the input's value. The positions and the caret are the same either way, so the
conformance holds: the corpus compares positions and texts with the prompt, and the field's text is a
function of them. Open question 2 asks the owner.

`MaskedInput`'s signature, imitating `MoneyInput`:

```csharp
public MaskedInput(string mask, string value, Action<MaskedText>? onChanged, string label = "",
    string? placeholder = null, string? helper = null, string? error = null, Icons? leading = null,
    SizeVariant size = SizeVariant.Large, VisualNode? trailing = null, bool asciiOnly = false,
    InputTheme? style = null)
// init: Disabled, Autofocus, LabelHidden, OnFocusChanged
```

- `value` is read as `MaskedTextBox.Text` is set (`Replace(value, 0, Length)`), so the app may hold either the
  raw value or the formatted one: measured, `12345678901` and `123,456,789-01` both draw the same CPF in
  pt-BR. Held raw is the advice, since it does not depend on the culture.
- `onChanged` hands a `MaskedText`: `Value` (no literals, no prompts: `ToString(false, false)`), `Text` (with
  literals: what the person reads), `MaskCompleted` and `MaskFull`, .NET's names.
- What is typed outlives a re-render while the page's value says the same thing (`MoneyInput`'s rule): the
  field keeps its text when `Fit(fresh value)` equals it, so a parent that rebuilds on every key does not move
  the caret.
- The entry's `Kind` is derived from the mask: `Number` when every edit position is `0` or `9`, `Text`
  otherwise. The formatter says the field is not prose, so no target autocorrects, spell-checks or capitalizes
  it (decision 5).
- Validation is the app's call while typing (TextInput's B9 rule): when the field is LEFT holding text that
  does not complete the mask, it shows `SdkStrings.MaskIncomplete` as its error, unless the app gives one.

Which `MaskedTextProvider` options a developer sets: `AsciiOnly` only (a typed `bool`, .NET's name). The SDK
fixes the rest, each for a reason: `PromptChar` `_` (only the placeholder shows it), `AllowPromptAsInput`
`true`, `ResetOnPrompt` and `ResetOnSpace` `true` (the text-to-state rule needs them), `SkipLiterals` `true`
(typing or pasting a literal steps over it), and `IncludeLiterals`/`IncludePrompt` are not options at all,
since `MaskedText` carries both forms. The culture is the page's (`CultureInfo.CurrentCulture`), which the
server sets per request and the browser installs before hydration.

### 5. Editing on each target

**The web.** `lowerTextEntry` (and its C# twin, which declares the same attributes) installs, for an entry with
a formatter, a `beforeinput` handler that cancels the edit, builds the `TextEdit` from `selectionStart`,
`selectionEnd`, `inputType` and `data`, asks the formatter, writes `value` and `setSelectionRange`, and calls
`onChanged` itself (a cancelled `beforeinput` fires no `input`):

| `inputType` | edit |
| --- | --- |
| `insertText` | `Typed`, the selection, `data` |
| `insertFromPaste`, `insertFromDrop`, `insertReplacementText`, `insertFromYank` | `Pasted`, the selection, the text |
| `deleteContentBackward` / `deleteContentForward`, `deleteByCut`, `deleteByDrag` | `DeletedBackward` / `DeletedForward`, the selection or one position |
| `deleteWordBackward` / `deleteWordForward` | the run of edit positions up to the nearest literal (open question 8) |
| `deleteSoftLine*`, `deleteHardLine*` | to the start or the end of the field |
| `historyUndo`, `historyRedo` | cancelled, nothing happens (`MaskedTextBox` has no undo) |
| `insertCompositionText` | not cancelable: the composition rule below |

Measured in Chrome 157 and Firefox 159 (`measure/browser/`): `beforeinput` is cancelable for `insertText`,
`insertFromPaste` and `deleteContentBackward`, and a value and a selection written inside the cancelled event
are what the field shows, caret included, in both. `getTargetRanges()` is empty for an `<input>` in both, so
the range comes from the selection, never from the event. The other road, rewriting in `input` after the
browser edited, is measured wrong: the caret jumps to the end (typing `9` at 2 of `123.456.78` leaves it at
11), and Backspace over a literal is undone by the reformat (the stuck Backspace every hand-written mask has).
After the script wrote the value, `Meta+Z` produced nothing in either browser.

**The composition rule, one for every target:** a composition is never formatted while it is composed. The
field keeps the text and selection it had when the composition started; the browser (or the host) draws the
composition as it always does; when it ends, the field goes back to the kept state and the committed text is
applied as one `Typed` edit, which is what winforms does (an IME's result arrives as `WM_CHAR`s, each through
`PlaceChar`). Measured in Chrome (CDP's IME): `insertCompositionText` is not cancelable and arrives with
`isComposing`, the browser inserts the composition raw (`１２___…`), and `compositionend` carries the
committed text with no `input` after it. In Firefox, Playwright's `insertText` arrives as a composition
whose `input` comes after `compositionend` with `isComposing` false, so the handler acts on `compositionend`
and ignores that `input`. A real IME in Firefox and in Safari is not measured (see the report).

**Photon.** `PhotonHost` asks the entry's formatter at each funnel, before `Commit`: `TextInput(text)` as
`Typed` over the selection, the paste as `Pasted`, Backspace and Delete with their kinds (the word and line
variants as on the web), cut as `DeletedForward` over the selection, `CommitText` as `Typed`, and
`SetMarkedText` composes without formatting, by the same composition rule. `UpdateEditingValue` (iOS and
Android) derives one edit from the whole new value against the current one: the common prefix and suffix
give the range and the text; an empty text with the new caret at the range's start after a caret at its end is
`DeletedBackward`, at its start `DeletedForward`; one inserted character is `Typed`, more is `Pasted`; a value
with a composing range is kept as the keyboard sent it until the range clears. The answer goes back to the
keyboard by the path that exists: `TextInputSession` is polled after each frame and hands the platform the
field's new state. The shells change in one line each: `PhotonKeyboard` (iOS) and `PhotonInputConnection`
(Android) treat a field whose formatter is not prose as not prose (no autocorrection, no suggestions, no
capitalization: `UITextAutocorrectionType.No`, `TYPE_TEXT_FLAG_NO_SUGGESTIONS`), which also keeps a Latin
keyboard from composing letters, so a plate's letters reach the engine one by one. The keyboard of a mask of
digits is `TextEntryKind.Number`, which already opens `UIKeyboardType.NumberPad` and `InputTypes.ClassNumber`
(and `inputmode="numeric"` on the web). #675 is stale on that point: its keyboard half was delivered by
`TextEntryKind` (#1764), and the autofill hints it also asks for are #1762.

### 6. The server validates with `MaskedTextProvider` itself

`eQuantic.UI.Primitives` gains `[ServerOnly] public sealed class MaskAttribute(string mask) :
ValidationAttribute`, with `AsciiOnly`. Its `IsValid` builds a `System.ComponentModel.MaskedTextProvider`
with the SDK's fixed options in `CultureInfo.CurrentCulture`, and a value is valid when it is null or empty
(as every DataAnnotations rule but `[Required]`), or when the text-setter composite places every character
and the mask is completed (measured: `12345678901` and `123,456,789-01` pass in pt-BR, `1234567890` does
not, and `123.456.789-01` does not in pt-BR but does in the invariant culture). It lives in Primitives
because a shared model project references Primitives for `[FormModel]` already, and because it needs no
engine. Nothing like it exists in .NET or in the tree (searched: no `MaskAttribute`, no use of
`MaskedTextProvider`). The server validates the .NET way, `Validator.TryValidateObject`, which an app already
calls in a server action.

The browser validates the same model through `[FormModel]`: `FormModelGenerator` reads `MaskAttribute` (its
namespace is checked like DataAnnotations', so an app's own `[Mask]` is not mistaken for it) and emits
`Rules.Custom(value => value.Length == 0 || new MaskFormatter("…").Read(value).MaskCompleted, message)`, the
same shape it emits for `[RegularExpression]`. The engine runs it under the page's culture.

### 7. Accessibility

The field's value is what it draws, with no prompts (decision 4). What it still asks for is the entry's
DESCRIPTION: `MaskedInput` adds `SdkStrings.MaskRemaining(count)` ("3 more characters required", in each
culture the SDK speaks) to the helper or error caption while the mask is not completed, and nothing once it
is. On the web that is the `.eq-desc` twin the entry already carries (`aria-describedby`). On Photon
`SemanticNode` has no description today (`TextEntry.Description` says "native fence"), so the change gives it
one, `Description`, and each shell sets its platform's: `accessibilityHelp` (macOS), `accessibilityHint`
(iOS), `AccessibilityNodeInfo.setHintText` (Android), UI Automation's `HelpText` (Windows). Every field's
helper and error reach assistive technology on Photon with it, not only a mask's. An incomplete field that was
left is invalid (`aria-invalid` on the web); Photon's invalid bit stays fenced, as `TextEntry.Invalid` says.

### 8. SSR and hydration

The server draws `MaskedInput` with the C# engine: the value fitted, the placeholder, `inputmode`, and no
handler (SSR emits none). The browser's first build draws it with the transpiled engine under the same
culture, so the two are the same text, and the conformance suite is what guarantees it. A field nobody used
is not touched by hydration (`showWhatThePageHolds` writes only a different value, and the value is equal).
A field a person typed into before the runtime loaded keeps what was typed (hydration-contract,
"A field the person used before the runtime loaded"), which a mask did not shape: when it hydrates, a used
formatted field is fitted once, as a paste over its whole text, with the caret where the fit ends, and the
page hears it through `onChanged`. Open question 6 asks the owner.

### Principles followed and bent

Followed: 1 (the developer writes a mask in .NET's language and nothing of a platform), 2 (the capability is
a field whose edits pass a rule; the mask is one), 3 (.NET's language, .NET's names, DataAnnotations), 4 (one
engine, both targets, both sides executed), 5 (formatter, edit, typed, pasted, prose), 6 (Flutter's
`TextInputFormatter`, improved by taking the edit), 9 (a corpus that fails, regenerated behind
`EQ_UPDATE_MASK_CORPUS`; the gap in eqc fixed rather than worked around), 13 (`TextEditingValue` moves, the old
place goes), 14 (`DateSeparator`), 15 (`MaskedInput` composed from `TextInput`; the formatter is a capability
any field takes).

Bent: `MaskedTextBox`'s LOOK (the prompts are the placeholder, not drawn in the text), for assistive
technology and because the web cannot draw what is not in the value; its behaviour is kept.

## Risks / Trade-offs

- **JavaScript's Unicode tables are not .NET's.** `\p{Nd}` and `\p{L}` in the browser's ICU may classify a
  rare character differently from .NET's tables. → The seeded sessions draw from the classes' edges
  (fullwidth, Arabic-Indic, combining marks, letters outside the BMP), and a difference is a runtime defect.
- **A culture's data can differ between hosts** (the ICU version .NET finds on macOS, Linux and Windows), and
  the corpus is measured on one. → The corpus test runs on the three CI platforms; a culture that differs is
  dropped from the corpus or pinned per platform, never silently rewritten.
- **A position is a UTF-16 unit**, in `MaskedTextProvider` as in the engine; a character outside the BMP is two
  positions and `L` refuses each half. → Kept: it is .NET's behaviour, pinned by a corpus case.
- **Writing the value cancels the browser's own undo and its autocorrect underline.** → Accepted:
  `MaskedTextBox` has neither.
- **A phone keyboard that composes anyway** (a CJK keyboard on a letter mask) shows its composition raw until
  it commits. → The composition rule; measured on no device (the report says so).
- **The culture decides the literals.** A model validated on a server running another culture than the page's
  reads `.` differently. → `[Mask]` reads `CultureInfo.CurrentCulture`, which the request culture sets; the
  docs advise holding the raw value, which no separator touches.

## Migration Plan

`eQuantic.UI.Native.Components.TextEditingValue` becomes `eQuantic.UI.Primitives.TextEditingValue` (same
members). The migration line: replace `using eQuantic.UI.Native.Components;` with
`using eQuantic.UI.Primitives;` where `TextEditingValue` is named (only a shell or a test names it). Nothing
else an app writes changes.

## Open Questions

1. The owner's example `MaskedInput(mask: "000.000.000-00", …)` draws commas in pt-BR. Is .NET's reading kept
   (and the docs teach `000\.000\.000-00`), as recommended?
2. Unfilled positions: drawn as they are typed with the prompt shape as placeholder (recommended), prompts
   while focused (`HidePromptOnLeave`), or prompts always (`MaskedTextBox`'s default)?
3. `onChanged`'s type: `Action<MaskedText>` with a `string value` (recommended), a `MaskedText` both ways, or
   `string` both ways plus a completion callback?
4. The names: `ITextInputFormatter`, `TextEdit`, `TextEditKind` (`Typed`, `Pasted`, `DeletedBackward`,
   `DeletedForward`), `MaskFormatter`, `MaskedText` (`Value`, `Text`, `MaskCompleted`, `MaskFull`),
   `MaskAttribute`, `SemanticNode.Description`.
5. A paste with spaces garbles a mask, measured, because .NET reads a space as a reset. Kept exactly
   (recommended), or the paste first drops the characters the mask cannot place anywhere?
6. A field typed into before hydration: fitted when it hydrates (recommended), or left until the next edit?
7. `Obscure` on `MaskedInput` (a PIN with literals): fenced out of 1.0 (recommended), or offered with the
   literals obscured too (what the web's password input can do)?
8. A word deletion (Alt+Backspace) deletes the run of positions up to the nearest literal (recommended), to the
   start of the field, or one position?
