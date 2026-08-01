import { describe, it, expect } from 'vitest';
import { lowerVisualNode, tokenValue } from './lowering';
import { hashDeclaration } from './style-atomizer';
import { photonTheme } from './design-system.generated';
import { StyleChannels } from './value-types';
import type { LoweringContext } from './lowering';
import type { AnchoredNode, ColorTokenValue, VisualNodeValue } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };

/** The atomic class the C# sink would hash for a `transition:<value>` declaration. */
function transitionClass(value: string): string {
  return `eq-${hashDeclaration(`transition:${value}`)}`;
}

function classesOf(node: { attributes: Record<string, string | undefined> }): string[] {
  return (node.attributes['class'] ?? '').split(' ');
}

const text = { nodeKind: 'text', content: 't', role: 'label' };

function anchored(extra: Partial<AnchoredNode> = {}): AnchoredNode {
  return {
    nodeKind: 'anchored',
    anchor: text,
    panel: { nodeKind: 'text', content: 'p', role: 'label' },
    ...extra,
  } as AnchoredNode;
}

/**
 * Spec S6 style transitions — the emitted string is the C# TokenCss.Transition twin (the
 * S6TransitionRealizerTests pin the same literals on the .NET side).
 */
describe('S6 transitions (C# cross-pin)', () => {
  it('colors covers every color property, gradient layer included', () => {
    const node = lowerVisualNode(
      {
        nodeKind: 'box',
        style: { transition: { channels: StyleChannels.colors, durationMs: 150 } },
      } as unknown as VisualNodeValue,
      ctx,
    );

    const expected =
      'background-color 150ms cubic-bezier(0.2, 0, 0, 1), ' +
      'background-image 150ms cubic-bezier(0.2, 0, 0, 1), ' +
      'border-color 150ms cubic-bezier(0.2, 0, 0, 1), ' +
      'color 150ms cubic-bezier(0.2, 0, 0, 1), ' +
      'fill 150ms cubic-bezier(0.2, 0, 0, 1), ' +
      'stroke 150ms cubic-bezier(0.2, 0, 0, 1)';
    expect(classesOf(node)).toContain(transitionClass(expected));
  });

  it('combined channels emit in the declared order', () => {
    const node = lowerVisualNode(
      {
        nodeKind: 'box',
        style: {
          transition: {
            channels: StyleChannels.opacity | StyleChannels.transform,
            durationMs: 200,
          },
        },
      } as unknown as VisualNodeValue,
      ctx,
    );

    expect(classesOf(node)).toContain(
      transitionClass(
        'opacity 200ms cubic-bezier(0.2, 0, 0, 1), transform 200ms cubic-bezier(0.2, 0, 0, 1)',
      ),
    );
  });

  it('delay rides the third slot only when set', () => {
    const node = lowerVisualNode(
      {
        nodeKind: 'box',
        style: {
          transition: {
            channels: StyleChannels.opacity,
            durationMs: 100,
            delayMs: 40,
            easing: [0, 0, 0, 1],
          },
        },
      } as unknown as VisualNodeValue,
      ctx,
    );

    expect(classesOf(node)).toContain(transitionClass('opacity 100ms cubic-bezier(0, 0, 0, 1) 40ms'));
  });

  it('a gradient via midpoint lands as the third stop at its position', () => {
    const node = lowerVisualNode(
      {
        nodeKind: 'box',
        style: {
          gradient: {
            from: photonTheme.surface,
            to: photonTheme.background,
            direction: 'toBottomRight',
            via: photonTheme.surfaceSubtle,
            viaPosition: 0.4,
          },
        },
      } as unknown as VisualNodeValue,
      ctx,
    );

    // The declaration rides an atomic class, so assert through the same hash the C# sink computes
    // (theme colors are var-rewritten with their literal fallback, exactly like ThemeVarMap does).
    const v = (name: string, token: ColorTokenValue) =>
      `var(--eq-color-${name}, ${tokenValue(token)})`;
    const value =
      `linear-gradient(to bottom right, ${v('surface', photonTheme.surface)},` +
      ` ${v('surface-subtle', photonTheme.surfaceSubtle)} 40%,` +
      ` ${v('background', photonTheme.background)})`;
    expect(classesOf(node)).toContain(`eq-${hashDeclaration(`background-image:${value}`)}`);
  });
});

/** Anchored open/close motion — panel and scrim stay MOUNTED and glide (C# LowerAnchored twin). */
describe('anchored motion (C# cross-pin)', () => {
  it('keeps the panel mounted while closed, hidden and lifted toward the anchor', () => {
    const host = lowerVisualNode(
      anchored({ motion: { channels: StyleChannels.opacity, durationMs: 300 } }),
      ctx,
    );

    expect(host.children).toHaveLength(2);
    const panel = host.children[host.children.length - 1] as { attributes: Record<string, string | undefined> };
    const classes = classesOf(panel);
    expect(classes).toContain(`eq-${hashDeclaration('opacity:0')}`);
    expect(classes).toContain(`eq-${hashDeclaration('visibility:hidden')}`);
    expect(classes).toContain(`eq-${hashDeclaration('pointer-events:none')}`);
    expect(classes).toContain(`eq-${hashDeclaration('transform:translateY(-8px)')}`);
    expect(classes).toContain(
      transitionClass(
        'opacity 300ms cubic-bezier(0.2, 0, 0, 1), transform 300ms cubic-bezier(0.2, 0, 0, 1), ' +
          'visibility 300ms cubic-bezier(0.2, 0, 0, 1)',
      ),
    );
  });

  it('open clears the closed state but keeps the glide', () => {
    const host = lowerVisualNode(
      anchored({ open: true, motion: { channels: StyleChannels.opacity, durationMs: 200 } }),
      ctx,
    );

    const panel = host.children[host.children.length - 1] as { attributes: Record<string, string | undefined> };
    const classes = classesOf(panel);
    expect(classes).not.toContain(`eq-${hashDeclaration('opacity:0')}`);
    expect(classes).not.toContain(`eq-${hashDeclaration('visibility:hidden')}`);
    expect(classes).toContain(
      transitionClass(
        'opacity 200ms cubic-bezier(0.2, 0, 0, 1), transform 200ms cubic-bezier(0.2, 0, 0, 1), ' +
          'visibility 200ms cubic-bezier(0.2, 0, 0, 1)',
      ),
    );
  });

  it('cross-fades the scrim and keeps the anchor lifted while closed', () => {
    const host = lowerVisualNode(
      anchored({
        onDismiss: () => {},
        scrimStyle: { background: photonTheme.scrim },
        motion: { channels: StyleChannels.opacity, durationMs: 300 },
      }),
      ctx,
    );

    expect(host.children).toHaveLength(3);
    const anchor = host.children[0] as { attributes: Record<string, string | undefined> };
    expect(classesOf(anchor)).toContain('eq-anchor-above');
    const scrim = host.children[1] as { attributes: Record<string, string | undefined> };
    const classes = classesOf(scrim);
    expect(classes).toContain(`eq-${hashDeclaration('opacity:0')}`);
    expect(classes).toContain(`eq-${hashDeclaration('visibility:hidden')}`);
    expect(classes).toContain(
      transitionClass(
        'opacity 300ms cubic-bezier(0.2, 0, 0, 1), visibility 300ms cubic-bezier(0.2, 0, 0, 1)',
      ),
    );
  });
});
