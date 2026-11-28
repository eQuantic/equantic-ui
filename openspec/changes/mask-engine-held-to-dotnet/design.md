## Context

This is the SHARED design of the masked field. Five OpenSpec changes ship it, one per pull request, and each of
the later four cites the decisions below by number rather than copying them:

| Change | Pull request | Sections (sub-issues) | Decisions it ships |
| --- | --- | --- | --- |
| `mask-engine-held-to-dotnet` (this one) | A | 1 to 7 (#1968 to #1974) | 1 (the vocabulary), 2, 3, the engine's half of 4, 7 and 8 |
| `a-formatter-decides-a-web-fields-edits` | B | 8 and 9 (#1975, #1976) | The web's half of 5, 6 and 8 |
| `a-formatter-decides-a-photon-fields-edits` | C | 10 and 11 (#1977, #1978) | Photon's half of 5, 6 and 8 |
| `field-takes-the-shape-of-its-mask` | D | 12, 13, 14, 17 and 18 (#1979 to #1983) | 4, 7's component and server halves, 9, 11 and 12 |
| `a-server-action-validates-its-arguments` | E | 15 and 16 (#1984) | 10, designed there |

A, then B and C side by side, then D. E is independent of the other four.

#1966 asks for a field that takes the shape of a mask (a CPF, a phone number, a postal code, a plate or a
card's expiry), on the web and on Photon, written in .NET's own mask language. The owner settled these on
2026-11-28, and they are not reopened here:

- the component is `MaskedInput`;
- the language is `System.ComponentModel.MaskedTextProvider`'s, with `MaskedTextBox`'s syntax;
- it enters 1.0, and so does everything `MaskedTextBox` does: overwrite mode, `PasswordChar`,
  `ValidatingType`, and an undo and redo it does not have;
- the engine is an assembly of its own, `eQuantic.UI.Masking`, as Code and Sheet are;
- server actions validate their arguments' DataAnnotations automatically, and `[Mask]` is one attribute among
  them (designed in `a-server-action-validates-its-arguments`);
- the seven questions of the design's second round, recorded under "Decisions the owner took" below.

The general capability under the request is wider than masks: **a field whose edits pass through a rule
before they land**, and which can therefore move the caret, refuse a character, rewrite what was pasted,
overtype, obscure what was entered and not what the rule wrote, and keep its own history. The mask is the
first rule. The vocabulary gains the hook, and the mask is one implementation of it.

What the tree has today, read before designing:

- **`TextEntry`** (vocabulary) carries a value and an `OnChanged(string)`.
  - On the web it lowers to a real `<input>`, and the BROWSER owns the caret, the selection and the IME. The
    runtime listens to `input` only (`lowering.ts`, `lowerTextEntry`).
  - On Photon, `PhotonHost` owns editing. Every edit reaches one of `TextInput(text)`, `EditKey` (Backspace,
    Delete, cut, paste), `SetMarkedText`/`CommitText` (a desktop composition) and `UpdateEditingValue` (a
    phone's keyboard, Flutter's whole `TextEditingValue`), and all of them end in `Commit(entry, value,
    caret)`.
  - An obscured Photon field is drawn as `•` for every character (`EmitVisitor.Text.cs:204`). It copies
    nothing and cuts by deleting (`PhotonHost.cs:1141`, `1823`). It opens the secure keyboard on iOS
    (`SecureTextEntry`) and `TextVariationPassword` on Android.
  - No Photon field has an undo, and no shell has an Undo menu item or an undo manager (searched: no `undo` in
    any shell).
- **The input family:**
  - `TextInput`, the design system's field, composes a `TextEntry`.
  - `NumberInput` and `MoneyInput` (#1764) are stateful components that keep what is typed in a buffer while
    the page's value "says" the same thing (`MoneyInput.AdoptConfig`).
  - `TextEntryKind` says what a field takes, and each target derives its keyboard from it.
- **The write-once engines:** `eQuantic.UI.Code` and `eQuantic.UI.Sheet`, C# transpiled into the runtime and
  driven by both realizers through a vocabulary interface. Each is wired in nine places (decision 2), and held
  together by twin tests that run the C# under .NET and its twin in the served bundle under the embedded Bun
  (`tests/eQuantic.UI.Conformance.Tests/Code*TwinTests.cs`). `CodeHistory` coalesces runs of typing into one
  step and drops the redo branch on a new step.
- **Forms:** `[FormModel]` turns a model's DataAnnotations into a `FormController` at build time
  (`FormModelGenerator`). `FormController.SubmitAsync` turns a throw into `SubmitError`, and
  `ApplyServerErrors(IReadOnlyList<FieldError>)` puts a server's verdict onto the fields, but nothing calls it
  for the developer.
- **Server actions:** they do not validate their arguments (`ServerActionsMiddleware.cs`). A refusal is a status
  code with `{ success: false, error }`, which the client's `server-actions.ts` throws as a plain `Error`
  carrying the message.

## Goals / Non-Goals

**Goals:**

- One mask engine, in its own assembly, transpiled into the runtime, held to `MaskedTextProvider` and to
  `MaskedTextBox`'s editing by a corpus measured from .NET and pinned, executed on both sides.
- A vocabulary hook every host calls before an edit lands, plus the commands every host routes to it (undo,
  redo, overwrite), so the engine is written once and the hosts only translate their events.
- `MaskedInput`, composed from `TextInput`. It hands on the raw value, the formatted one and whether the mask is
  complete. It overwrites, obscures entered positions while the literals stay visible, parses into a typed
  value, and undoes and redoes.
- Server validation with `MaskedTextProvider` itself, through a DataAnnotations attribute, and server actions
  that validate every argument's DataAnnotations without the developer writing a line.
- The web, SSR and hydration included, and Photon on macOS, iOS, Android and Windows.
- Assistive technology hears what the field holds and what it still asks for, on both targets.

**Non-Goals:**

- `MaskedTextBox`'s events beyond what a value carries (`MaskInputRejected`, `TypeValidationCompleted`). The
  refusal is visible as nothing landing, and the typed validation is the field's error (decision 7).
- `BeepOnError`. No target here beeps at a field.
- `CutCopyMaskFormat` and `TextMaskFormat` as options. `MaskedText` carries both forms, and a copy takes what
  is drawn (decision 4).
- A per-field screenshot protection. No platform's own password field has one (decision 6).

## How Flutter solves it

The question is about Flutter's FIRST tree and the `State` behind it.

- A `TextField` widget takes `inputFormatters`, a list of `TextInputFormatter`. `EditableTextState` runs every
  edit the platform reports (`TextInputClient.updateEditingValue`) through them before the controller takes
  it: `TextEditingValue formatEditUpdate(TextEditingValue oldValue, TextEditingValue newValue)`. The
  `mask_text_input_formatter` package is one such formatter.
- `UndoHistory`, a widget `EditableText` builds, keeps `TextEditingValue`s. It is driven by the platform's undo
  (`UndoManager` on iOS, the shortcuts elsewhere) through `UndoHistoryController`.
- `obscureText` obscures every character; Flutter has no field that obscures part of its text.
- Overwrite: Flutter has none.
- docs/FLUTTER-PARITY.md rows 118 and 119 record that `TextEditingValue` and `TextInputSession` already follow
  Flutter on Photon, and row 170 records `TextEntryKind` against `TextInputType`.

Doing better than Flutter on two points:

- **The formatter is handed the EDIT rather than a second value.** A formatter that compares two whole values
  must guess the edit (was `11` → `1` the first or the second digit? a Backspace or a Delete?), and
  `mask_text_input_formatter` guesses. Every host here already knows the edit, and `MaskedTextBox` decides
  differently for a Backspace and a Delete that remove nothing (measured).
- **The history lives with the formatter.** It is the formatter that knows a value it wrote from a value the page
  wrote, which is the event that must cut the history (decision 8).

## Decisions

### 1. The vocabulary: a formatter on `TextEntry`, and the commands hosts route to it

`eQuantic.UI.Primitives` gains:

```csharp
public interface ITextInputFormatter
{
    /// The field's state after the edit, or `current` itself when the edit is refused.
    TextEditingValue Format(TextEditingValue current, TextEdit edit);

    /// The field's state after the command, or null when the formatter does not take it (the host then does
    /// what it does without a formatter).
    TextEditingValue? Command(TextEditingValue current, TextCommand command);

    /// Whether what the field takes is prose (spelling, autocorrection, a capital first letter).
    bool Prose { get; }

    /// Whether a typed character replaces the one after the caret: the caret is drawn as a block where the
    /// target can.
    bool Overwriting { get; }

    /// Whether the character at `index` of `text` is the formatter's own rather than one that was entered:
    /// an obscured field draws it as it is.
    bool IsLiteral(string text, int index);
}

public readonly record struct TextEdit(int Start, int End, string Text, TextEditKind Kind);
public enum TextEditKind : byte { Typed = 0, Pasted = 1, DeletedBackward = 2, DeletedForward = 3 }
public enum TextCommand : byte { Undo = 0, Redo = 1, ToggleOverwrite = 2 }
```

- **`TextEntry` gains `Formatter`** (`ITextInputFormatter?`).
- **`TextEditingValue` moves** from `eQuantic.UI.Native.Components` to Primitives, unchanged: both targets now
  hand it, and its names are Flutter's.
- **`TextInput` and `FormInput`** take `formatter` as a constructor parameter and hand it to their entry.
- **A developer may write their own formatter**: a field that upper-cases, a card number in groups of four.
  `MaskFormatter` is the SDK's one.

Why an interface on the node, and not a `Mask` property on `TextEntry`: a `Mask` on the vocabulary would make
both realizers know masks, and each would call the engine itself. The interface keeps them knowing ONE
protocol, as `ICodeSurfaceModel` does, and the mask stays in its library (principles 4 and 15).

The names would exist without the web (principle 5): a formatter, an edit, typed, pasted, undo, redo,
overwrite, literal, prose.

Why `TextEditKind` is needed, measured:

- A Backspace after the dot of `123.4__` and a Delete before it remove the same position. `MaskedTextBox` leaves
  the caret at 3 after the Backspace and at 4 after the Delete, when nothing is removed.
- `Typed` and `Pasted` are two composites in `MaskedTextBox` (`OnKeyPress` → `PlaceChar` per character, and
  `Replace(string, start, length)`), and they differ when a character is refused.

`TextCommand` is a door of its own, not an edit, because undo, redo and the Insert key change no text by
themselves. Each host maps its own keys and gestures onto the three (decision 8), and a formatter that does not
take one answers null.

### 2. The engine: `eQuantic.UI.Masking`, an assembly of its own, wired as Code and Sheet are

This is the owner's decision. The wiring is what `eQuantic.UI.Code` and `eQuantic.UI.Sheet` have, place by place,
read in the tree:

| Where | What Code and Sheet have | What Masking gets |
| --- | --- | --- |
| `src/eQuantic.UI.<Engine>/<Engine>.csproj` | `net10.0`, `IsAotCompatible`, a `ProjectReference` to Primitives, `PackageId` and description | The same, without `InvariantGlobalization` (Sheet sets it; the mask reads cultures) |
| `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` | Declared, with RS0016/RS0017 | Declared |
| Surface class (`Directory.Build.targets`) | Derived from the name: `contract` | Derived: `contract`; `scripts/check-package-surface.cs` reads it without a line |
| `src/eQuantic.UI.Sdk/Sdk/Sdk.props` and `src/eQuantic.UI.Sdk.Native/Sdk/Sdk.props` | A `PackageReference` at `$(EQuanticUIVersion)`, and a `ProjectReference` beside a source tree (`IsEQuanticDevMode`) | The same pair in both SDKs: the developer's csproj writes nothing |
| `RuntimeProvidedTypeScanner.VocabularyNamespaces` and `.RuntimeAssemblies` | Both lists name the engine | Both name `eQuantic.UI.Masking`; `VocabularyInterfaceDefaultsTests` holds the second |
| `SharedComponentTranspilationTests.EngineSources` | Each project directory, recursively | Masking added; its transpiled modules committed under `src/eQuantic.UI.Runtime/src/shared/components/` (`EQ_UPDATE_TRANSPILED=1`) |
| `eQuantic.UI.Web.Build` (`EnumUnionsTsGenerator`) | A `ProjectReference` and the engines named, so their enums cross as string unions | The same, for `InsertKeyMode` and the hint enum |
| `tests/eQuantic.UI.Conformance.Tests` (csproj, `Infrastructure/ModuleGraph.cs`) | A `ProjectReference`, and the assembly in the module graph's references | The same |
| `eQuantic.UI.sln` | An entry | An entry |
| The component that uses it | `eQuantic.UI.Components` references Code; `eQuantic.UI.Spreadsheets` references Sheet | Components references Masking, for `MaskedInput` |

The SDK puts `eQuantic.UI.Components.UI` in scope as a static using (`Sdk.props:183`). `MaskedText` and the
parsers are named by an app, so the SDK adds `<Using Include="eQuantic.UI.Masking" />` beside it. Without it a
page would have to write the `using`, and a doc example would fail without it.

**The assembly's public surface:**

- `MaskFormatter : ITextInputFormatter`.
- `MaskedText`.
- `InsertKeyMode`.
- `IMaskedValueParser` and `MaskedValueParser<TValue>` (decision 7).
- `[ServerOnly] MaskAttribute` (decision 9).

`MaskAttribute` lives here rather than in Primitives because it is the mask's, and the assembly references only
Primitives, so a shared model project can reference it as cheaply. The engine itself is
`internal sealed class MaskEngine`, visible to the conformance tests through `InternalsVisibleTo`.

**What `MaskEngine` holds:**

- The parsed mask: a descriptor per position, with the culture's literals resolved at construction, as
  `MaskedTextProvider` resolves them.
- The provider's operations, by .NET's own names: `Add`, `InsertAt`, `Replace`, `RemoveAt`, `Remove`, `Set`,
  `Clear`, `ToString(bool, bool)`, `ToDisplayString`, `FindEditPositionFrom`, `FindAssignedEditPositionFrom`,
  `FindUnassignedEditPositionFrom`, `IsEditPosition`, `VerifyEscapeChar`, `MaskCompleted`, `MaskFull` and
  `LastAssignedPosition`.
- The hints `MaskedTextResultHint` reports, as an internal enum.
- `MaskedTextBox`'s composites over them: `Type` with `overwrite`, `Delete(backward)`, `Paste` and `SetText`.
  They are transcribed from winforms `MaskedTextBox.cs` at `3248f2c4b8839706952bb7d863a0b2b957b745f7` and cited
  there.

```csharp
public sealed class MaskFormatter(string mask, bool asciiOnly = false,
    InsertKeyMode insertKeyMode = InsertKeyMode.Default) : ITextInputFormatter
{
    public string Mask { get; }
    public bool AsciiOnly { get; }
    public InsertKeyMode InsertKeyMode { get; }
    public bool IsOverwriteMode { get; }           // MaskedTextBox's name
    public MaskedText Read(string text);           // the values a field's text holds
    public string Fit(string value);               // MaskedTextBox's Text setter: a value fitted to the mask
    // ITextInputFormatter: Format, Command, Prose => false, Overwriting => IsOverwriteMode, IsLiteral
}

public enum InsertKeyMode : byte { Default = 0, Insert = 1, Overwrite = 2 }   // MaskedTextBox's enum, by its names

public readonly record struct MaskedText(string Value, string Text, bool MaskCompleted, bool MaskFull);
```

**The engine's STATE is the field's text.** `Format` rebuilds the provider's state from the text, position by
position (the text a field draws is aligned to the mask, decision 4), applies the one edit, and writes the text
back. It is lossless because the SDK fixes `ResetOnSpace` and `ResetOnPrompt` to `true`, .NET's defaults: a
space or a prompt in the text reads back as an empty position. Measured:

- `set:1 2345678` on `(99) 9999-9999` gives `(1_) 2345-678_`;
- `set:1 3` on `00000-000` gives `1_3__-___`.

What the formatter keeps BESIDE the text is its history and its overwrite mode (decision 8), which is why
`MaskedInput` keeps one `MaskFormatter` per instance across rebuilds.

**What the engine reads of .NET, and the gap it meets.**

- `MaskedTextProvider` reads `.` as the culture's `NumberDecimalSeparator`, `,` as `NumberGroupSeparator`, `$`
  as `CurrencySymbol`, `/` as `DateTimeFormat.DateSeparator` and `:` as `TimeSeparator`.
- It classifies with `char.IsDigit`, `char.IsLetter`, `char.IsLetterOrDigit` and the culture's
  `ToUpper`/`ToLower`.
- eqc carries the char methods (`CharMethodStrategy`, Unicode classes) and `NumberFormatInfo`'s members
  (`PlatformMemberRead`), but NOT `DateTimeFormatInfo.DateSeparator` and `TimeSeparator`, although the runtime's
  culture data has both (`utils/culture.ts`).

That is a gap of the SDK (principle 14). It is closed in this change, as its own slice, rather than worked
around in the engine with a hardcoded `/`.

### 3. Holding the engine to .NET: a corpus measured, pinned, and executed twice

The truth is .NET, measured (PROVING §3), in two layers:

1. **`MaskedTextProvider`'s operations**, run on the real class.
   - The operations: `Add`, `InsertAt`, `Replace` (a char or a string, at a position or over a range),
     `RemoveAt` (one position or a range), `Remove`, `Set` and `Clear`.
   - After each: the state (`ToString` with every flag, the password-ignoring overload, `ToDisplayString` with
     a `PasswordChar`, `MaskCompleted`, `MaskFull`, the counts, `LastAssignedPosition`), the result, the test
     position and the hint.
   - For every position: `IsEditPosition`, and the `Find…PositionFrom` methods in both directions.
2. **`MaskedTextBox`'s editing composites**: typing a character in insert and overwrite mode, Backspace, Delete,
   paste, and setting the text.
   - They are winforms code over the provider, and they cannot run here (WinForms is Windows-only).
   - The test project carries them transcribed over the REAL `MaskedTextProvider`, cited at the winforms commit,
     so the oracle for every primitive under them is still .NET's.

The corpus is a JSON file pinned in the conformance project (`Masks/masked-text-provider.corpus.json`),
generated by a test from the real class:

- Without `EQ_UPDATE_MASK_CORPUS=1`, the test regenerates it in memory and FAILS on any difference: a .NET
  servicing change, or a corpus case edited by hand.
- With the variable, it writes the file.

Three runs read it: the real `MaskedTextProvider` (the regeneration), the C# `MaskEngine` under .NET, and the
engine's twin in the served bundle under the embedded Bun, as `CodeLinesTwinTests` runs the code engine. Beside
the pinned cases, seeded random sessions compare the three live (masks drawn from the corpus's, edits drawn from
the four kinds and the overwrite mode over random selections), so the corpus is not the only question asked.

The draft corpus is measured already: `measure/measure.cs`, 71 cases, on .NET 10.0.9. Its findings decide this
design:

- **`.` and `,` are the culture's separators, not literals.**
  - `000.000.000-00` in pt-BR is drawn `___,___,___-__`, and a CPF typed into it reads `123,456,789-01`.
    `Set("123.456.789-01")` fails at position 4 with `DigitExpected`.
  - A CPF is `000\.000\.000-00` in .NET's language.
  - `$999,999.00` is `R$___.___,__` in pt-BR and `€___.___,__` in de-DE. `00/00/0000` is `__.__.____` in
    de-DE. `00:00` is `__.__` in fi-FI.
- **The caret skips a literal on the NEXT character, not after the last one.** After `1`, `2`, `3` in a CPF the
  caret stands at 3, before the separator, and `4` lands at 4 with the caret at 5. Typing the separator itself
  at 2 of `00/00/0000` steps over it (caret 3), because `SkipLiterals` is on.
- **A deletion keeps the literals in place and shifts what follows.** Backspace right after the separator, with
  digits after it, removes nothing and puts the caret before the separator. The next Backspace removes `3` and
  shifts the rest left (`123,456,789-01` → `124,567,890-1_`).
- **Overwrite replaces without shifting, and also skips a literal.**
  - `9` at 0 of a full CPF gives `923,…` with the caret at 1.
  - `8` at 3 (the separator) writes at 4 with the caret at 5.
  - At the end, a full field refuses with `UnavailableEditPosition`.
  - In insert mode a full field refuses every character, which is what makes overwrite worth having on a fixed
    shape such as a date.
- **A paste is fitted character by character, and a refused character is skipped.**
  - `123.456.789-01` pasted into a pt-BR CPF is refused twice (the dots) and still completes the mask.
  - `+55 11 98765-4321` pasted into `(00) 00000-0000` gives `(55) _11_9-8765`, because a space is a reset (it
    empties a position).
- **Optional positions keep their gap.** In `(99) 9999-9999`, the raw value of `(1_) 2345-678_` is `1 2345678`,
  with the space, and the mask is complete with no position assigned at all.
- **Any Unicode decimal digit is a digit, `AsciiOnly` or not.** `１` (fullwidth) and `١` (Arabic-Indic) fill a
  `0`. `AsciiOnly` refuses `é` and `ç` in an `L`.
- **A `PasswordChar` obscures assigned positions and nothing else.** `0000-0000` with `*` holding `12345` is
  drawn `****-*___`: the literal and the prompts stay.
- **`AllowPromptAsInput` only matters with `ResetOnPrompt` off**, and the SDK keeps `ResetOnPrompt` on.

### 4. What the field draws, and the values it hands on

The field draws `ToString(includePrompt: false, includeLiterals: true)` cut after the last assigned position.

- The shape grows as it is typed (`1`, `12`, `123`, `123.4`), and an empty field is empty, so its placeholder
  shows. The placeholder defaults to the mask's prompt shape (`___.___.___-__`). The owner took this.
- Unassigned positions before the last assigned one are spaces, which keeps the text aligned to the mask position
  by position (decision 2).
- A copy takes what is drawn. An obscured field copies nothing (decision 6).

`MaskedInput`'s signature, imitating `MoneyInput`:

```csharp
public MaskedInput(string mask, string value, Action<MaskedText>? onChanged, string label = "",
    string? placeholder = null, string? helper = null, string? error = null, Icons? leading = null,
    SizeVariant size = SizeVariant.Large, VisualNode? trailing = null, bool asciiOnly = false,
    InsertKeyMode insertKeyMode = InsertKeyMode.Default, IMaskedValueParser? validatingType = null,
    InputTheme? style = null)
// init: Disabled, Autofocus, LabelHidden, Obscure, OnFocusChanged
```

- **`value`** is read as `MaskedTextBox.Text` is set (`Replace(value, 0, Length)`), so the app may hold either the
  raw value or the formatted one. Measured: `12345678901` and `123,456,789-01` both draw the same CPF in pt-BR.
  Holding the raw value is the advice, since it does not depend on the culture.
- **`onChanged`** hands a `MaskedText`, in .NET's names:
  - `Value`: no literals, no prompts (`ToString(false, false)`);
  - `Text`: with the literals, what the person reads;
  - `MaskCompleted` and `MaskFull`.
- **What is typed outlives a re-render** while the page's value says the same thing (`MoneyInput`'s rule): the
  field keeps its text when `Fit(fresh value)` equals it, so a parent that rebuilds on every key does not move the
  caret.
- **The entry's `Kind` is derived from the mask**: `Number` when every edit position is `0` or `9`, and `Text`
  otherwise. The formatter says the field is not prose, so no target autocorrects, spell-checks or capitalizes it
  (decision 5).
- **Validation while typing is the app's call** (TextInput's B9 rule). When the field is LEFT holding text that
  does not complete the mask, it shows `SdkStrings.MaskIncomplete` as its error, unless the app gives one. A
  completed text that `validatingType` cannot parse shows `SdkStrings.MaskValueInvalid` (decision 7).

**The options a developer sets**, typed and by .NET's names:

- `AsciiOnly`;
- `InsertKeyMode`;
- `ValidatingType`;
- `Obscure`, the target's own password glyph standing for `PasswordChar` and `UseSystemPasswordChar`.

**What the SDK fixes, each for a reason:**

| Option | Fixed at | Why |
| --- | --- | --- |
| `PromptChar` | `_` | Only the placeholder shows it |
| `AllowPromptAsInput` | `true` | .NET's default |
| `ResetOnPrompt`, `ResetOnSpace` | `true` | The text-to-state rule needs them |
| `SkipLiterals` | `true` | Typing or pasting a literal steps over it |
| `IncludeLiterals`, `IncludePrompt` | not options at all | `MaskedText` carries both forms |

The culture is the page's (`CultureInfo.CurrentCulture`), which the server sets per request and the browser
installs before hydration.

### 5. Editing on each target

**The web.**

`lowerTextEntry` and its C# twin, which declares the same attributes, install a `beforeinput` handler for an
entry with a formatter. The handler:

1. cancels the edit;
2. builds the `TextEdit` from `selectionStart`, `selectionEnd`, `inputType` and `data`;
3. asks the formatter;
4. writes the answer through `document.execCommand('insertText')` over the field's whole text, under a guard
   against its own `beforeinput`, then calls `setSelectionRange`;
5. calls `onChanged` itself, because a cancelled `beforeinput` fires no `input`.

Writing through `execCommand` rather than assigning `value` keeps the browser's undo stack ARMED, so the
browser's own undo entry points (the Edit menu, the context menu, iOS's shake and three-finger gestures) reach
the page as `historyUndo` and `historyRedo` (decision 8).

The edit each input type becomes:

| `inputType` | Edit |
| --- | --- |
| `insertText` | `Typed`, over the selection, with `data` |
| `insertFromPaste`, `insertFromDrop`, `insertReplacementText`, `insertFromYank` | `Pasted`, over the selection, with the text |
| `deleteContentBackward` / `deleteContentForward`, `deleteByCut`, `deleteByDrag` | `DeletedBackward` / `DeletedForward`, over the selection or one position |
| `deleteWordBackward` / `deleteWordForward` | The run of edit positions up to the nearest literal (the owner took this) |
| `deleteSoftLine*`, `deleteHardLine*` | To the start or the end of the field |
| `historyUndo`, `historyRedo` | Cancelled, and handed to the formatter as `TextCommand.Undo` and `Redo` |
| `insertCompositionText` | Not cancelable: the composition rule below |

`keydown` hands the formatter `Ctrl/Cmd+Z` (`Undo`), `Ctrl/Cmd+Shift+Z` and `Ctrl+Y` (`Redo`), and `Insert` with
no modifier (`ToggleOverwrite`), and cancels each key the formatter took. While `Overwriting`, the input carries
`caret-shape: block`.

Measured in Chrome 157 and Firefox 159 (`measure/browser/`):

- `beforeinput` is cancelable for `insertText`, `insertFromPaste` and `deleteContentBackward`. A value and a
  selection written inside the cancelled event are what the field shows, caret included, in both browsers.
- `getTargetRanges()` is empty for an `<input>` in both, so the range comes from the selection, never from the
  event.
- The other road, rewriting in `input` after the browser edited, is measured wrong. The caret jumps to the end:
  typing `9` at 2 of `123.456.78` leaves it at 11. And Backspace over a literal is undone by the reformat, which
  is the stuck Backspace every hand-written mask has.
- After a script ASSIGNED the value, `Meta+Z` produced nothing in either browser: no `historyUndo` at all.
- After the same edits were written with `execCommand('insertText')`, `Meta+Z` fired a CANCELABLE `historyUndo`
  in both. Left alone, it brings back the previous text (`123.` → `12`, caret 2). Cancelled, the text stays, so
  the engine can decide. Once an undo was cancelled, `Meta+Shift+Z` fired no `historyRedo`, because the browser's
  own redo stack is empty. That is why redo is also taken from `keydown`.
- `caret-shape: block` is supported in Chrome 157 and not in Firefox 159, where overwrite mode draws the usual
  caret.

**The composition rule, one for every target.** A composition is never formatted while it is composed.

1. The field keeps the text and selection it had when the composition started.
2. The browser, or the host, draws the composition as it always does.
3. When the composition ends, the field goes back to the kept state, and the committed text is applied as one
   `Typed` edit. This is what winforms does: an IME's result arrives as `WM_CHAR`s, each through `PlaceChar`.

Measured:

- In Chrome (CDP's IME), `insertCompositionText` is not cancelable and arrives with `isComposing`. The browser
  inserts the composition raw (`１２___…`). `compositionend` carries the committed text, and no `input` follows
  it.
- In Firefox, Playwright's `insertText` arrives as a composition whose `input` comes after `compositionend`, with
  `isComposing` false. So the handler acts on `compositionend` and ignores that `input`.
- A real IME in Firefox and in Safari is not measured.

**Photon.** `PhotonHost` asks the entry's formatter at each funnel, before `Commit`:

| Funnel | What the formatter is handed |
| --- | --- |
| `TextInput(text)` | `Typed`, over the selection |
| The paste | `Pasted` |
| Backspace and Delete | Their kinds; the word and line variants as on the web |
| Cut | `DeletedForward`, over the selection |
| `CommitText` | `Typed` |
| `SetMarkedText` | Nothing: it composes without formatting, by the same composition rule |
| `Cmd/Ctrl+Z`, `Cmd/Ctrl+Shift+Z`, `Ctrl+Y` and `Insert` in `EditKey` | The three commands |

`UpdateEditingValue` (iOS and Android) derives one edit from the whole new value against the current one:

- the common prefix and suffix give the range and the text;
- an empty text, with the new caret at the range's start after a caret at its end, is `DeletedBackward`; at its
  start, `DeletedForward`;
- one inserted character is `Typed`, and more is `Pasted`;
- a value with a composing range is kept as the keyboard sent it, until the range clears.

The answer goes back to the keyboard by the path that exists: `TextInputSession` is polled after each frame and
hands the platform the field's new state. Photon draws its own caret, so overwrite mode draws a block the width
of the character after it.

The shells' keyboards change in one line each:

- `PhotonKeyboard` (iOS) and `PhotonInputConnection` (Android) treat a field whose formatter is not prose as not
  prose: `UITextAutocorrectionType.No`, `TYPE_TEXT_FLAG_NO_SUGGESTIONS`. That also keeps a Latin keyboard from
  composing letters, so a plate's letters reach the engine one by one.
- The keyboard of a mask of digits is `TextEntryKind.Number`, which already opens `UIKeyboardType.NumberPad` and
  `InputTypes.ClassNumber`, and `inputmode="numeric"` on the web.
- #675 is stale on that point: its keyboard half was delivered by `TextEntryKind` (#1764), and the autofill hints
  it also asks for are #1762.

**A phone has no Insert key.** `InsertKeyMode` is .NET's answer, and it serves the phone:

- `Default` toggles with Insert where a keyboard has one: Windows, Linux, a PC keyboard on a Mac, a hardware
  keyboard on Android.
- `Insert` and `Overwrite` hold the mode whatever is pressed, so a developer who wants a date overtyped on a
  phone says `InsertKeyMode.Overwrite`.
- iPad hardware keyboards have no Insert key. Android sends `KEYCODE_INSERT` from a hardware keyboard, and its
  shell hands it to `EditKey` like any key it forwards.

### 6. Obscure, to `MaskedTextBox`'s `PasswordChar` bar

`MaskedTextBox` with a `PasswordChar` draws the assigned positions obscured and the literals as they are
(measured, `****-*___`). The bar the owner set: do this only if the field keeps what a password field gives on
each target. Those things are no autocorrect, no copy out, secure entry on phones, password managers, and screen
capture protection where the platform has it. What each target's own password field gives was read and
measured. A design was then sought that keeps it.

**Photon: kept whole, at no cost.**

- Photon draws its own text. `EmitVisitor.Text` draws `•` for every character of an obscured field today, and
  draws `•` only where `Formatter.IsLiteral` says no.
- Everything else stays as it is:
  - the copy refusal (`PhotonHost.Copyable` answers `""` for an obscured field);
  - the cut that only deletes;
  - `SecureTextEntry` on iOS;
  - `TextVariationPassword` (or `NumberVariationPassword` for a mask of digits) on Android.
- Screen capture: no Photon field is protected today. iOS's secure `UITextField` hides its own layer from
  captures, but Photon draws its text itself and only proxies the keyboard. Android's `FLAG_SECURE` and Windows'
  `SetWindowDisplayAffinity` are a whole window's, not a field's, and the platforms' own password fields do not
  set them.
- So nothing a password field gives is lost by showing the literals. The iOS gap is an existing one, filed as a
  candidate in the report.

**The web: two designs measured, and the overlay keeps both.**

- **A. A real `type="password"` input, holding the whole text (literals included), with the literals drawn over
  it.** An overlay of `aria-hidden`, `pointer-events: none` spans sits inside the field's box. Each one draws
  its literal on the field's own fill, over the bullet that stands for it.
  - Measured in Chrome 157 and Firefox 159:
    - a password input's bullets are all one width, whatever the characters: 25 characters of `1`, `W`, `.`
      or `1-1-…` are all 181 px wide at 16 px in both browsers. So a literal's place is its index times the
      measured bullet width, less the input's `scrollLeft`.
    - A cancelled `beforeinput` and a written value and selection work on a password input in both browsers:
      `1-2-`, caret 4.
    - Chrome sends no `copy` event for `Meta+C` on a password field, and the clipboard keeps what it held.
      Firefox fires `copy` and refuses `execCommand('copy')` (`false`). Whether Firefox writes anything is not
      measured.
  - **Kept:** it IS a password input. The password managers see `type="password"` and its `autocomplete`. The
    browsers refuse to copy out. Phones open their secure keyboard. The browsers' secure event input on macOS
    stays (WebKit's and Chrome's own behaviour for password fields, a claim not measured here).
  - **Lost:**
    - A literal stands in a bullet's cell, so the field's spacing is the bullets' (7.24 px at 16 px) rather than
      the font's. A multi-character currency symbol takes as many cells as it has characters.
    - The overlay has to follow the input's horizontal scroll. Measured: `setSelectionRange` does not scroll the
      input when it is called, so the overlay observes the input's `scroll` (PROVING §11) rather than
      computing once.
    - A screen reader hears a secure field of N characters, without the literals. The description still says
      what the mask asks for (decision 10).
- **B. A text input holding bullets, with the real characters kept in memory.**
  - **Lost:** every protection a password field has. There is no password manager (they look for
    `type="password"`), no secure keyboard on iOS and Android, and no secure event input on macOS. An IME
    composes in the clear. The real value is in no element, so SSR cannot serve it, the hydration contract
    cannot keep what was typed before the runtime loaded, and the browser's autofill and form restore see
    bullets.
  - **Kept:** the literals at their real width.
- **C. `-webkit-text-security: disc` on a text input.** It is supported in Chrome 157 and Firefox 159 (measured,
  `CSS.supports`). It obscures the whole element, literals included, so it cannot reach the bar. It also offers
  none of a password input's protections beyond the glyph.

**Decision: A.** It keeps both the literals and the protections, so there was no trade-off left for the owner.
The residual cost is the spacing, which is a matter of look, and the owner accepted it for 1.0 (owner's decision 1
below).

### 7. `ValidatingType`: the text parsed into a typed value, as the culture writes it

How `MaskedTextBox` does it (winforms source):

- A `Type` is set as `ValidatingType`.
- When the field is left, `PerformTypeValidation` parses `ToString(includePrompt: false, IncludeLiterals)` with
  `Formatter.ParseObject`, which is the type's `TypeConverter` in the culture.
- It raises `TypeValidationCompleted` with `IsValidInput` and the `ReturnValue`, and an incomplete mask is
  invalid before any parsing.

A `Type` cannot cross to the browser, which has no reflection. A generic `MaskedInput<TValue>` would collide with
`MaskedInput` in JavaScript, whose classes are not generic, and the factory surface allows no overloads. And
`TValue.TryParse` through a static abstract member needs a receiver that erasure takes away. So the type is
carried by a value:

```csharp
public interface IMaskedValueParser { bool CanParse(string text, IFormatProvider provider); }

public sealed class MaskedValueParser<TValue>(MaskedValueParser<TValue>.TryParseHandler tryParse) : IMaskedValueParser
{
    public delegate bool TryParseHandler(string text, IFormatProvider? provider, out TValue value);
    public bool CanParse(string text, IFormatProvider provider);
    public bool TryParse(MaskedText text, out TValue value);   // parses `Text` in the current culture
}
```

The delegate is the shape of `IParsable<T>.TryParse(string, IFormatProvider?, out T)`, so every parsable type of
.NET hands its own method group: `new MaskedValueParser<DateOnly>(DateOnly.TryParse)`. A page holds one, hands it
to the field and reads with it:

```csharp
static readonly MaskedValueParser<DateOnly> Day = new(DateOnly.TryParse);
MaskedInput(mask: "00/00/0000", value: text, validatingType: Day,
    onChanged: v => SetState(() => { text = v.Value; day = Day.TryParse(v, out var d) ? d : null; }))
```

- **The field** shows `SdkStrings.MaskValueInvalid` when it is left completed but unparsable, as
  `TypeValidationCompleted` reports `IsValidInput` false.
- **The server agrees** through `[Mask(…, ValidatingType = typeof(DateOnly))]`. It calls the type's own static
  `TryParse(string, IFormatProvider, out T)`, found by reflection, in the current culture: the method the
  browser's parser calls, rather than `MaskedTextBox`'s `TypeConverter`, so that both sides run the same parse.
- Measured on .NET 10.0.9 (`measure/parse.cs`), `TypeConverter` and `DateOnly.TryParse` agree on the cases tried:
  - `28/11/2026`, `1/1/2026` and `28/11/26` in pt-BR;
  - `11/28/2026` in en-US;
  - `28.11.2026` in de-DE;
  - and on refusing `31/02/2026` in pt-BR and `28/11/2026` in en-US.
  - `int` agrees on `12345` and ` 1234`, and on refusing `1 234`. The converter throws `ArgumentException`
    there, and `TryParse` answers false.
- **The conformance suite holds the three together** for the date, time and number types: the converter, the
  `TryParse` on the server, and the twin's `TryParse` in the browser.
- **The generator:** `[FormModel]` with `ValidatingType` emits a rule over a
  `MaskedValueParser<T>(global::T.TryParse)`.
- **Not measured:** that eqc carries a BCL method group with an `out` parameter into a custom generic delegate.
  The slice's first task measures it, and if it fails, the gap is the compiler's to close (principle 14).

### 8. Undo and redo: the engine's history, reached by every target's own gesture

`MaskedTextBox` has no undo: `CanUndo` is `false`, and `Undo()` and `ClearUndo()` are empty (winforms source).
The owner wants one, and the browser's own history cannot be it: it only knows what the browser wrote, and it is
not on Photon.

`MaskFormatter` keeps a history of `TextEditingValue`s, after `CodeHistory`'s rules, made smaller by what a mask
is:

- A run of typed characters is one step until a deletion, a paste, a caret moved elsewhere or a pause breaks it.
- A new step drops the redo branch.
- The limit is 100 steps.
- Undo and redo answer the value to show, text and selection, and the page hears it through `onChanged`.
- **The history is cut when the page writes the value.** When `Format` or `Command` is handed a `current` that
  is not the last value the formatter answered, the field was changed from outside (a form reset, a page that
  rewrote it). The history is cleared, so undo never brings back what the page took away.

How each target's gesture reaches it:

| Target | Keys | Menu and gestures |
| --- | --- | --- |
| Web, every browser | `keydown` `Ctrl/Cmd+Z`, `Ctrl/Cmd+Shift+Z`, `Ctrl+Y`, cancelled and routed | The Edit menu, the context menu and iOS Safari's shake and three-finger swipe dispatch `historyUndo`/`historyRedo`, because the writes go through `execCommand` (measured: cancelable in both browsers). The browser's Redo menu item stays disabled after an undo the engine made: a measured limit |
| Photon macOS | `EditKey` `Cmd+Z`, `Cmd+Shift+Z` | The shell's main menu gains Edit › Undo and Redo, sent to the host as the commands while a text target holds the caret. Today the shell has no such item |
| Photon Windows | `Ctrl+Z`, `Ctrl+Y`, `Ctrl+Shift+Z` | None: a Win32 field has no menu of its own |
| Photon iOS | A hardware keyboard's `Cmd+Z` | `PhotonKeyboard`'s proxy answers `undoManager` with an `NSUndoManager` whose actions call the host's commands, so the shake and the three-finger gestures reach the formatter, as Flutter's `UndoManager` channel does |
| Photon Android | A hardware keyboard's `Ctrl+Z`, `Ctrl+Shift+Z` | Android has no system undo gesture. An IME's own undo arrives as a whole value and is an edit like another |

A field with no formatter keeps what each target does today. A plain `TextEntry` with a history is a separate
capability, not asked here.

### 9. The server validates with `MaskedTextProvider` itself

`eQuantic.UI.Masking` gains `[ServerOnly] public sealed class MaskAttribute(string mask) : ValidationAttribute`,
with `AsciiOnly` and `ValidatingType`.

- **`IsValid`** builds a `System.ComponentModel.MaskedTextProvider`, with the SDK's fixed options, in
  `CultureInfo.CurrentCulture`. A value is valid when:
  - it is null or empty, as with every DataAnnotations rule but `[Required]`; or
  - the text-setter composite places every character, the mask is completed, and the `ValidatingType`, when
    there is one, parses it.
- Measured:
  - `12345678901` and `123,456,789-01` pass in pt-BR;
  - `1234567890` does not;
  - `123.456.789-01` does not in pt-BR, but does in the invariant culture.
- Through .NET 10's `Microsoft.Extensions.Validation`, the same attribute reports `Cpf=[…]` and, on a nested
  model, `Address.PostalCode=[…]` (measured, `measure/validation/`).
- Nothing like it exists in .NET or in the tree (searched: no `MaskAttribute`, and no use of
  `MaskedTextProvider`).

The browser validates the same model through `[FormModel]`. `FormModelGenerator` reads `MaskAttribute`, checked
by its namespace as DataAnnotations' are, so an app's own `[Mask]` is not mistaken for it. It emits
`Rules.Custom(message, value => value.Length == 0 || new MaskFormatter("…").Read(value).MaskCompleted)`, the
shape it emits for `[RegularExpression]`. The engine runs it under the page's culture.

### 10. Server actions validate their arguments (shipped by `a-server-action-validates-its-arguments`)

Server-action validation is general and does not depend on the mask: it is designed in its own change,
`a-server-action-validates-its-arguments` (#1984). `[Mask]` is one attribute it validates, once
`field-takes-the-shape-of-its-mask` ships it.

### 11. Accessibility

- **The value** is what the field draws, with no prompts (decision 4).
- **What it still asks for** is the entry's DESCRIPTION. While the mask is not completed, `MaskedInput` adds
  `SdkStrings.MaskRemaining(count)` ("3 more characters required", in each culture the SDK speaks) to the helper
  or error caption, and adds nothing once it is completed.
- **On the web**, that is the `.eq-desc` twin the entry already carries (`aria-describedby`). An obscured field's
  overlay is `aria-hidden`.
- **On Photon**, `SemanticNode` has no description today (`TextEntry.Description` says "native fence"). The change
  gives it one, `Description`, and each shell sets its platform's:
  - `accessibilityHelp` on macOS;
  - `accessibilityHint` on iOS;
  - `AccessibilityNodeInfo.setHintText` on Android;
  - UI Automation's `HelpText` on Windows.

  With it, every field's helper and error reach assistive technology on Photon, not only a mask's.
- **An incomplete field that was left is invalid**: `aria-invalid` on the web. Photon's invalid bit stays fenced,
  as `TextEntry.Invalid` says.

### 12. SSR and hydration

- **The server** draws `MaskedInput` with the C# engine: the value fitted, the placeholder, `inputmode`, the
  password input for an obscured field with its overlay of literals, and no handler (SSR emits none).
- **The browser's first build** draws it with the transpiled engine under the same culture, so the two are the
  same text. The conformance suite is what guarantees it.
- **A field nobody used** is not touched by hydration: `showWhatThePageHolds` writes only a different value, and
  the value is equal.
- **A field a person typed into before the runtime loaded** keeps what was typed (hydration-contract, "A field
  the person used before the runtime loaded"), which a mask did not shape. When it hydrates, a used formatted
  field is fitted once, as a paste over its whole text, with the caret where the fit ends, and the page hears it
  through `onChanged`. The owner took this.

### Principles followed and bent

**Followed:**

- 1: the developer writes a mask in .NET's language and nothing of a platform.
- 2: the capability is a field whose edits pass a rule; the mask is one.
- 3: .NET's language and names, DataAnnotations, `ValidatingType`, `InsertKeyMode`, `IParsable`'s shape.
- 4: one engine and one history, on both targets, both sides executed.
- 5: formatter, edit, command, literal, prose.
- 6: Flutter's `TextInputFormatter` and `UndoHistory`, improved by taking the edit.
- 9: a corpus that fails, regenerated behind `EQ_UPDATE_MASK_CORPUS`, and the gap in eqc fixed.
- 13: `TextEditingValue` moves, and the old place goes.
- 14: `DateSeparator`, and the out-delegate if it fails.
- 15: `MaskedInput` composed from `TextInput`; the formatter is a capability any field takes.

**Bent:**

- `MaskedTextBox`'s LOOK, which draws the prompts always, gives way to a placeholder, for assistive technology
  and because the web cannot draw what is not in the value.
- On the web, an obscured masked field's spacing is the bullets'.

## Risks / Trade-offs

- **JavaScript's Unicode tables are not .NET's.** `\p{Nd}` and `\p{L}` in the browser's ICU may classify a rare
  character differently from .NET's tables. → The seeded sessions draw from the classes' edges, and a difference
  is a runtime defect.
- **A culture's data can differ between hosts** (the ICU version .NET finds on macOS, Linux and Windows), and the
  corpus is measured on one. → The corpus test runs on the three CI platforms. A culture that differs is dropped
  from the corpus or pinned per platform, never silently rewritten.
- **A position is a UTF-16 unit**, in `MaskedTextProvider` as in the engine. A character outside the BMP is two
  positions, and `L` refuses each half. → Kept: it is .NET's behaviour, pinned by a corpus case.
- **`document.execCommand` is deprecated.** It is still the only way to write an input with the browser's undo
  stack. → If a browser drops it, the field falls back to assigning `value`, and only the menu and gesture undo is
  lost (the keys still reach the engine). The fallback is tested by a vitest case with `execCommand` absent.
- **A phone keyboard that composes anyway** (a CJK keyboard on a letter mask) shows its composition raw until it
  commits. → The composition rule. Measured on no device.
- **The culture decides the literals.** A model validated on a server running another culture than the page's
  reads `.` differently. → `[Mask]` reads `CultureInfo.CurrentCulture`, which the request culture sets. The docs
  advise holding the raw value, which no separator touches.

## Migration Plan

- **`eQuantic.UI.Native.Components.TextEditingValue`** becomes `eQuantic.UI.Primitives.TextEditingValue`, with
  the same members. Migration line: name it from `eQuantic.UI.Primitives`. Only a shell or a test names it.

## Decisions the owner took

The first round (2026-11-28) settled the name, the language, the drawing (the shape grows as it is typed, the
prompt shape is the placeholder), `Action<MaskedText>` with a `string value`, the hydration fit, the word deletion
up to the nearest literal, .NET's reading of `.` and `,`, Unicode digits kept as .NET keeps them, the assembly of
its own, and everything `MaskedTextBox` does entering 1.0. The second round:

1. An obscured masked field on the web stands its literals in the bullets' cells, and that spacing is accepted for
   1.0 (decision 6).
2. On a phone, `InsertKeyMode.Default` inserts, as in .NET, and a full field refuses a typed character until one is
   deleted (decision 5).
3. Server-action validation walks .NET's `Validator`, to be swapped for `Microsoft.Extensions.Validation` when its
   host API leaves evaluation (`a-server-action-validates-its-arguments`).
4. Automatic validation has no opt-out (`a-server-action-validates-its-arguments`).
5. `ValidatingType` is a parser value, `MaskedValueParser<T>(T.TryParse)` (decision 7).
6. The names stand: `ITextInputFormatter` (`Format`, `Command`, `Prose`, `Overwriting`, `IsLiteral`), `TextEdit`,
   `TextEditKind`, `TextCommand` (`Undo`, `Redo`, `ToggleOverwrite`), `MaskFormatter`, `MaskedText`,
   `InsertKeyMode`, `IMaskedValueParser`, `MaskedValueParser<TValue>`, `MaskAttribute`, `SemanticNode.Description`
   and `ServerValidationException`.
7. One OpenSpec change per pull request, each archived before its own merge, which is the table under Context.

## Open Questions

None.
