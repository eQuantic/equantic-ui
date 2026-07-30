import { describe, it, expect } from 'vitest';
import { atomizeEntries, hashDeclaration } from './style-atomizer';
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
      expect(`eq-${hashDeclaration(`${entry.prop}:${entry.rewritten}`)}`).toBe(entry.class);
    }
  });

  it('rewrites theme colors to the same var(--eq-color-*, fallback) as C#', () => {
    for (const entry of fixture as FixtureEntry[]) {
      const atomized = atomizeEntries({ [entry.prop]: entry.value });
      expect(atomized.class).toBe(entry.class);
    }
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
