/**
 * THE TRAILING-CONFIG CONTRACT (eqc cross-pin).
 *
 * eqc lowers a C# object initializer on a vocabulary type — `new Positioned(child) { Top = 8 }` —
 * to `new Positioned(child, null, null, null, null, { top: 8 })`: positional parameters first, then
 * ONE config object carrying whatever the initializer set. A twin whose constructor forgets that
 * trailing parameter silently drops it: the server renders the member (WebRealizer reads the C#
 * node) and the hydrated client renders it at its default. That is invisible in a unit test of
 * either side and shows up as a page that quietly loses a rule, an offset or a z-index the moment
 * it hydrates.
 *
 * This spec walks EVERY exported vocabulary class and proves the last parameter takes a config —
 * so a class added tomorrow cannot forget it.
 */

import { describe, expect, it } from 'vitest';
import * as vocabulary from './vocabulary';

/** Enough arguments to reach the config slot: the declared arity, then the probe. */
function construct(Klass: new (...args: unknown[]) => unknown, arity: number, probe: object) {
  const args = new Array(arity).fill(undefined);
  return Reflect.construct(Klass, [...args, probe]) as Record<string, unknown>;
}

/**
 * How many parameters the constructor declares. `Function.length` stops at the first default, so
 * the source is read for the real count — the config always rides the LAST slot.
 */
function declaredArity(Klass: { toString(): string }): number {
  const source = Klass.toString();
  const open = source.indexOf('constructor(');
  if (open === -1) return 0;
  let depth = 1;
  let i = open + 'constructor('.length;
  const start = i;
  while (depth > 0 && i < source.length) {
    const ch = source[i];
    if (ch === '(' || ch === '[' || ch === '{') depth++;
    else if (ch === ')' || ch === ']' || ch === '}') depth--;
    i++;
  }
  const header = source.slice(start, i - 1);
  if (header.trim().length === 0) return 0;
  // Split on top-level commas only — a config parameter's own type literal has commas inside.
  let level = 0;
  let count = 1;
  for (const ch of header) {
    if (ch === '(' || ch === '[' || ch === '{' || ch === '<') level++;
    else if (ch === ')' || ch === ']' || ch === '}' || ch === '>') level--;
    else if (ch === ',' && level === 0) count++;
  }
  // The config is the last declared parameter; the probe goes in its place.
  return count - 1;
}

/** Abstract bases are never constructed by transpiled code — only their concrete subclasses are. */
const abstractBases = new Set(['VisualNode', 'FlexNode']);

const classes = (Object.entries(vocabulary) as [string, unknown][])
  .filter(([name, value]) => typeof value === 'function' && /^class/.test(String(value)) && !abstractBases.has(name))
  .map(([name, value]) => [name, value as new (...args: unknown[]) => unknown] as const);

describe('vocabulary twins accept the trailing config eqc emits', () => {
  it('finds the vocabulary', () => {
    expect(classes.length).toBeGreaterThan(20);
  });

  for (const [name, Klass] of classes) {
    it(`${name} keeps what an object initializer sets`, () => {
      const arity = declaredArity(Klass);
      const probe = { eqProbe: 'kept' };
      let built: Record<string, unknown>;
      try {
        built = construct(Klass, arity, probe);
      } catch (error) {
        // A constructor that throws on undefined arguments (a guard) is not what this proves.
        expect.soft(String(error)).toBe('');
        return;
      }
      expect(built.eqProbe, `${name} dropped the trailing config — add it to its constructor`).toBe(
        'kept',
      );
    });
  }
});
