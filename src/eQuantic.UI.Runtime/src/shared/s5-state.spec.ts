import { describe, it, expect } from 'vitest';
import { lowerVisualNode } from './lowering';
import { hashDeclaration } from './style-atomizer';
import { photonTheme } from './design-system.generated';
import { tokenValue } from './lowering';
import type { LoweringContext } from './lowering';
import type { VisualNodeValue } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };

/** Spec S5 client lowering — pseudo-variant classes hashed IDENTICALLY to the C# sink. */
describe('S5 state overlays (C# cross-pin)', () => {
  it('hover diff appends the pseudo-variant class AFTER the base set (C# order parity)', () => {
    const subtle = tokenValue(photonTheme.surfaceSubtle);
    const node = lowerVisualNode(
      {
        nodeKind: 'box',
        style: {
          background: photonTheme.surface,
          hover: { background: photonTheme.surfaceSubtle },
          width: { kind: 'fixed', value: 10 },
          height: { kind: 'fixed', value: 10 },
        },
      } as unknown as VisualNodeValue,
      ctx,
    );

    // The C# sink hashes ":hover|background-color:<var-rewritten>" — reproduce the exact class.
    const rewritten = `var(--eq-color-surface-subtle, ${subtle})`;
    const expected = `eq-${hashDeclaration(`:hover|background-color:${rewritten}`)}`;
    const classes = (node.attributes['class'] ?? '').split(' ');
    expect(classes).toContain(expected);
    expect(classes[classes.length - 1]).toBe(expected, 'pseudo variants come last');
  });

  it('focus diff uses :focus-visible with the border shorthand', () => {
    const focusColor = tokenValue(photonTheme.focusRing);
    const node = lowerVisualNode(
      {
        nodeKind: 'box',
        style: { focus: { borderWidth: 2, borderColor: photonTheme.focusRing } },
      } as unknown as VisualNodeValue,
      ctx,
    );
    const rewritten = `2px solid var(--eq-color-focus, ${focusColor})`;
    const expected = `eq-${hashDeclaration(`:focus-visible|border:${rewritten}`)}`;
    expect((node.attributes['class'] ?? '').split(' ')).toContain(expected);
  });
});
