import { describe, it, expect } from 'vitest';
import { lowerVisualNode } from './lowering';
import { CONTROL_FOCUS, CONTROL_PRESSED, hashDeclaration, stateSelector } from './style-atomizer';
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
    // pseudo variants come last
    expect(classes[classes.length - 1]).toBe(expected);
  });

  it('focus diff lowers to the control focus family with the border shorthand', () => {
    const focusColor = tokenValue(photonTheme.focusRing);
    const node = lowerVisualNode(
      {
        nodeKind: 'box',
        style: { focus: { borderWidth: 2, borderColor: photonTheme.focusRing } },
      } as unknown as VisualNodeValue,
      ctx,
    );
    const rewritten = `2px solid var(--eq-color-focus, ${focusColor})`;
    // The CONTROL's focus (#508): the family's name is in the hash, as the C# sink's is.
    const expected = `eq-${hashDeclaration(`${CONTROL_FOCUS}|border:${rewritten}`)}`;
    expect((node.attributes['class'] ?? '').split(' ')).toContain(expected);
  });

  // #508: a focus and a press are the CONTROL's. The selectors are the C# StyleSink.StateSelector's,
  // character for character, and their specificity is the handoff's order: hover (0,2,0) under focus
  // (0,3,0) under pressed (0,4,0).
  it('focus and press select every box inside the control, ranked by specificity', () => {
    expect(stateSelector(':hover', 'eq-x')).toBe('.eq-x:hover');
    expect(stateSelector(CONTROL_FOCUS, 'eq-x')).toBe('.eq-pressable:focus-visible .eq-x');
    expect(stateSelector(CONTROL_PRESSED, 'eq-x')).toBe('.eq-pressable.eq-pressable:active .eq-x');
  });

  it('a pressed diff lowers to the pressed family, hashed by its name', () => {
    const node = lowerVisualNode(
      {
        nodeKind: 'box',
        style: { pressed: { transform: { translateX: 0, translateY: 0, rotationDegrees: 0, scaleX: 0.5, scaleY: 0.5 } } },
      } as unknown as VisualNodeValue,
      ctx,
    );
    const expected = `eq-${hashDeclaration('pressed|transform:scale(0.5)')}`;
    expect((node.attributes['class'] ?? '').split(' ')).toContain(expected);
  });
});
