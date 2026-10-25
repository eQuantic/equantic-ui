/**
 * The default members of the vocabulary's interfaces, as functions of the instance (#414).
 *
 * eqc writes each default a class takes into the class's twin, converted from the interface's
 * source. An app compiles against the vocabulary's ASSEMBLIES, where an interface has its signature
 * and not its body, so there the twin delegates here instead: a theme that relies on
 * `IAppTheme.Data` gets `get data() { return IAppTheme.data(this); }`. Each function is its C#
 * default's twin, and `VocabularyInterfaceDefaultsTests` (C#) with `interface-defaults.spec.ts`
 * hold every default of every vocabulary interface to having one here: a default this file lacks
 * is a member an app's twin would answer undefined for.
 */

import { CodeLanguageRules } from './components/CodeLanguageRules';
import { DataPalette } from './data-palette';
import type { SheetTheme } from './sheet-theme';
import { codeTokenColor, type AppTheme, type ColorToken, type FontFamily } from './value-types';

/** `IAppTheme`'s defaults. */
export const IAppTheme = {
  /** `ColorToken Selection => FocusRing.WithOpacity(0.28f);` — the focus colour, washed. */
  selection: (theme: AppTheme): ColorToken => theme.focusRing.withOpacity(0.28),
  /** `ColorToken? OnSelection => null;` — selected text keeps its own colour. */
  onSelection: (_theme: AppTheme): ColorToken | null => null,
  /** `ColorToken Placeholder => TextMuted;` */
  placeholder: (theme: AppTheme): ColorToken => theme.textMuted,
  /** `ColorToken ScrollbarThumb => TextMuted.WithOpacity(0.55f);` */
  scrollbarThumb: (theme: AppTheme): ColorToken => theme.textMuted.withOpacity(0.55),
  /** `ColorToken ScrollbarTrack => SurfaceSubtle;` */
  scrollbarTrack: (theme: AppTheme): ColorToken => theme.surfaceSubtle,
  /** `FontFamily? TextFamily => null;` — no text face of its own. */
  textFamily: (_theme: AppTheme): FontFamily | null => null,
  /** `FontFamily? MonoFamily => null;` — no code face of its own. */
  monoFamily: (_theme: AppTheme): FontFamily | null => null,
  /** `DataPalette Data => DataPalette.Default;` — the validated reference palette. */
  data: (_theme: AppTheme): DataPalette => DataPalette.default,
  /** `SheetTheme? Sheet => null;` — the spreadsheet's own colours, from the foundation. */
  sheet: (_theme: AppTheme): SheetTheme | null => null,
  /** `ColorToken Code(CodeTokenKind kind)` — a token's colour from the theme's own roles. */
  code: (theme: AppTheme, kind: string): ColorToken => codeTokenColor(theme, kind),
};

/** `ICodeLanguage`'s defaults. */
export const ICodeLanguage = {
  /** `CodeLanguageRules Rules => CodeLanguageRules.Default;` */
  rules: (_language: unknown): CodeLanguageRules => CodeLanguageRules.default,
  /** `IReadOnlyList<string> Keywords => [];` — a language that only wants colours offers no words. */
  keywords: (_language: unknown): string[] => [],
};

/** `ICodeCompletionProvider`'s defaults. */
export const ICodeCompletionProvider = {
  /** `IReadOnlyList<char> TriggerCharacters => [];` — a new list per read, as C#'s is. */
  triggerCharacters: (_provider: unknown): string[] => [],
  /** `Task<CodeCompletionItem> ResolveAsync(item, cancellation) => Task.FromResult(item);` */
  resolveAsync: <T>(_provider: unknown, item: T, _cancellation?: unknown): Promise<T> => Promise.resolve(item),
};

/** `ICodeSignatureProvider`'s defaults. */
export const ICodeSignatureProvider = {
  /** `IReadOnlyList<char> TriggerCharacters => ['('];` — a new list per read, as C#'s is. */
  triggerCharacters: (_provider: unknown): string[] => ['('],
  /** `IReadOnlyList<char> RetriggerCharacters => [','];` */
  retriggerCharacters: (_provider: unknown): string[] => [','],
};
