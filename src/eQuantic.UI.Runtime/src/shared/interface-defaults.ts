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
import { codeTokenColor, type AppTheme, type ColorToken } from './value-types';

/** `IAppTheme`'s defaults. */
export const IAppTheme = {
  /** `string? MonoFamily => null;` — no code face of its own. */
  monoFamily: (_theme: AppTheme): string | null => null,
  /** `DataPalette Data => DataPalette.Default;` — the validated reference palette. */
  data: (_theme: AppTheme): DataPalette => DataPalette.default,
  /** `ColorToken Code(CodeTokenKind kind)` — a token's colour from the theme's own roles. */
  code: (theme: AppTheme, kind: string): ColorToken => codeTokenColor(theme, kind),
};

/** `ICodeLanguage`'s defaults. */
export const ICodeLanguage = {
  /** `CodeLanguageRules Rules => CodeLanguageRules.Default;` */
  rules: (_language: unknown): CodeLanguageRules => CodeLanguageRules.default,
};

/** `ICodeCompletionProvider`'s defaults. */
export const ICodeCompletionProvider = {
  /** `IReadOnlyList<char> TriggerCharacters => ['.'];` — a new list per read, as C#'s is. */
  triggerCharacters: (_provider: unknown): string[] => ['.'],
};
