import { describe, it, expect } from 'vitest';
import { lowerVisualNode } from './lowering';
import { photonTheme } from './design-system.generated';
import { Hoverable, Box, BoxStyle } from './vocabulary';
import type { LoweringContext } from './lowering';
import type { VisualNodeValue } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };

/** Spec S5 programmable hover — the TS twin of the C# S5HoverableRealizerTests pins. */
describe('S5 hoverable lowering (C# cross-pin)', () => {
  it('wires mouseenter/mouseleave to the boolean callback', () => {
    const log: boolean[] = [];
    const node = lowerVisualNode(
      new Hoverable(
        new Box(new BoxStyle({ width: 10, height: 10 })),
        (entered) => log.push(entered),
      ) as unknown as VisualNodeValue,
      ctx,
    );

    expect(node.tag).toBe('div');
    (node.events['mouseenter'] as () => void)();
    (node.events['mouseleave'] as () => void)();
    expect(log).toEqual([true, false]);
  });

  it('anchored scrimStyle paints the dismiss scrim', () => {
    const node = lowerVisualNode(
      {
        nodeKind: 'anchored',
        anchor: { nodeKind: 'box' },
        panel: { nodeKind: 'box' },
        open: true,
        onDismiss: () => {},
        scrimStyle: {
          background: {
            light: { r: 5, g: 6, b: 13, a: 140 },
            dark: { r: 5, g: 6, b: 13, a: 140 },
          },
        },
      } as unknown as VisualNodeValue,
      ctx,
    );

    const scrim = node.children[1] as { attributes: Record<string, string>; children: unknown[] };
    expect(scrim.attributes['class']).toContain('eq-anchor-scrim');
  });

  it('hover-open panels carry the gap as padding (hover bridge)', () => {
    const node = lowerVisualNode(
      {
        nodeKind: 'anchored',
        anchor: { nodeKind: 'box' },
        panel: { nodeKind: 'box' },
        openOnHover: true,
        gap: 8,
      } as unknown as VisualNodeValue,
      ctx,
    );

    const panel = node.children[node.children.length - 1] as {
      attributes: Record<string, string>;
    };
    // The atomized class set must contain the padding atom, and no margin atom.
    expect(panel.attributes['class']).toContain('eq-anchor-panel');
    const style = panel.attributes['style'] ?? '';
    const classes = panel.attributes['class'];
    expect(style + classes).not.toContain('margin-top');
  });
});
