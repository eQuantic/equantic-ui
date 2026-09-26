/**
 * The runtime's copy of every vocabulary interface default, cross-pinned with
 * `VocabularyInterfaceDefaultsTests.cs`, which lists the defaults by reflection into the fixture.
 *
 * The twin of an app's theme, language or completion provider delegates a default it relies on to
 * this copy, because the app compiles against the vocabulary's assemblies and eqc has no body to
 * convert there (#414). A line with no function here is a member that app's twin answers
 * undefined for.
 */

import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import * as defaults from './interface-defaults';
import { photonTheme } from './design-system.generated';
import { DataPalette } from './data-palette';
import { codeTokenColor } from './value-types';

const lines = readFileSync('src/shared/__fixtures__/interface-defaults.txt', 'utf8')
  .split('\n')
  .filter((line) => line.length > 0);
/** `Interface.member`: a default the runtime carries. */
const fixture = lines.filter((line) => !line.startsWith('code|'));
/** `code|kind|light|dark`: the C# default `Code` over the reference theme, for every token kind. */
const codeColors = lines.filter((line) => line.startsWith('code|'));

/** A colour's channels as the fixture writes them. */
const rgba = (color: { r: number; g: number; b: number; a: number }): string =>
  `${color.r},${color.g},${color.b},${color.a}`;

describe('the vocabulary interface defaults (cross-pinned with VocabularyInterfaceDefaultsTests.cs)', () => {
  it('lists the defaults it is held to', () => {
    expect(fixture).toContain('ICodeLanguage.rules');
  });

  for (const line of fixture) {
    it(`carries ${line}`, () => {
      const [contract, member] = line.split('.');
      const copy = (defaults as unknown as Record<string, Record<string, unknown>>)[contract];
      expect(copy, `interface-defaults.ts exports no ${contract}`).toBeDefined();
      expect(typeof copy[member], `${contract} has no ${member}`).toBe('function');
    });
  }

  // `codeTokenColor` is a hand copy of `IAppTheme.Code`'s switch; every arm is held here, not one.
  it('holds a colour for every token kind', () => {
    expect(codeColors.length).toBeGreaterThanOrEqual(12);
  });
  for (const line of codeColors) {
    const [, kind, light, dark] = line.split('|');
    it(`colours ${kind} as C#'s default does`, () => {
      const token = codeTokenColor(photonTheme, kind);
      expect(`${rgba(token.light)}|${rgba(token.dark)}`).toBe(`${light}|${dark}`);
    });
  }

  it('answers each default as C# does', () => {
    expect(defaults.ICodeLanguage.rules({}).indentWidth).toBe(4);
    expect(defaults.ICodeCompletionProvider.triggerCharacters({})).toEqual(['.']);
    // A new list per read, as a C# expression-bodied member builds one.
    expect(defaults.ICodeCompletionProvider.triggerCharacters({})).not.toBe(
      defaults.ICodeCompletionProvider.triggerCharacters({}),
    );
    expect(defaults.IAppTheme.monoFamily(photonTheme)).toBeNull();
    expect(defaults.IAppTheme.data(photonTheme)).toBe(DataPalette.default);
    expect(defaults.IAppTheme.code(photonTheme, 'keyword')).toEqual(
      photonTheme.colors('primary').base,
    );
  });
});
