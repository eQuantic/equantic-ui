import { describe, it, expect } from 'vitest';
import { atomizeEntries, declarationFor, hashDeclaration } from './style-atomizer';
// The SAME fixture the C# StyleAtomizerTests pins (EQ_UPDATE_ATOMIC_FIXTURE=1): declaration →
// rewritten value → class. Replaying it here proves the two atomizers hash identically — the
// guarantee behind hydration-by-class-identity (SSR markup classes == client lowering classes).
import fixture from './style-atomizer.fixture.json';

interface FixtureEntry {
  prop: string;
  value: string;
  rewritten: string;
  class: string;
}

describe('style atomizer: C# ↔ TS twin (fixture cross-pin)', () => {
  it('hashes every canonical declaration to the C# class name', () => {
    for (const entry of fixture as FixtureEntry[]) {
      // Through declarationFor, not string concatenation: a vendor pair is ONE declaration written
      // twice, and the C# twin hashes the same paired text.
      expect(`eq-${hashDeclaration(declarationFor(entry.prop, entry.rewritten))}`).toBe(entry.class);
    }
  });

  it('rewrites theme colors to the same var(--eq-color-*, fallback) as C#', () => {
    for (const entry of fixture as FixtureEntry[]) {
      const atomized = atomizeEntries({ [entry.prop]: entry.value });
      expect(atomized.class).toBe(entry.class);
    }
  });

  // Mirrors C# TranslucentVariantOfATokenColor_IsNotCorruptedByPrefixRewriting.
  it('does not rewrite a token color that merely PREFIXES a longer hex literal', () => {
    // Any fixture entry that DID rewrite gives us a real token CSS string to build on.
    const rewritten = (fixture as FixtureEntry[]).find((e) => e.rewritten.startsWith('var('));
    expect(rewritten, 'the fixture must contain at least one rewritten token').toBeTruthy();
    const tokenCss = rewritten!.value;

    // A longer literal that merely STARTS with the token is a DIFFERENT color and must survive
    // untouched: rewriting the prefix strands the trailing digits outside the var() — invalid CSS
    // the browser drops, which is how a hero grid line of white-at-4% silently vanished in the
    // site dogfood. The class is the hash of `prop:rewrittenValue`, so an intact value proves it.
    const extended = `${tokenCss}0a`;
    expect(atomizeEntries({ 'background-color': extended }).class).toBe(
      `eq-${hashDeclaration(`background-color:${extended}`)}`,
    );

    // Same rule inside a composite value (the grid pattern's gradient layers).
    const gradient = `linear-gradient(to right, ${extended} 1px, transparent 1px)`;
    expect(atomizeEntries({ 'background-image': gradient }).class).toBe(
      `eq-${hashDeclaration(`background-image:${gradient}`)}`,
    );
  });

  it('sorts classes and keeps only custom properties inline', () => {
    const atomized = atomizeEntries({
      padding: '16px',
      gap: '8px',
      '--eq-x': '42px',
    });
    const classes = atomized.class.split(' ');
    expect(classes).toHaveLength(2);
    expect([...classes].sort()).toEqual(classes);
    expect(atomized.style).toBe('--eq-x: 42px');
  });

  it('memoizes: the same declaration never inserts twice', () => {
    const a = atomizeEntries({ padding: '16px' }).class;
    const b = atomizeEntries({ padding: '16px' }).class;
    expect(a).toBe(b);
  });
});
