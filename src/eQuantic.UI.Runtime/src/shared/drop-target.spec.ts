import { describe, it, expect } from 'vitest';
import { lowerVisualNode } from './lowering';
import type { LoweringContext } from './lowering';
import { photonTheme } from './design-system.generated';
import type { VisualNodeValue } from './nodes';
import { Box, BoxStyle, DropTarget } from './vocabulary';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };

/** The TS twin of the C# DropTargetRealizerTests: the same markup, and the drag handlers attached. */
describe('DropTarget lowering (C# cross-pin)', () => {
  const box = () => new Box(new BoxStyle({ width: 10, height: 10 }));

  it('is a layout-transparent div marked as an enabled drop area, with the four drag handlers', () => {
    const node = lowerVisualNode(new DropTarget(box(), () => {}) as unknown as VisualNodeValue, ctx);

    expect(node.tag).toBe('div');
    expect(node.attributes['data-eq-drop']).toBe('on');
    expect(Object.keys(node.events).sort()).toEqual(['dragenter', 'dragleave', 'dragover', 'drop']);
    expect(node.children).toHaveLength(1);
  });

  it('says it is off when disabled', () => {
    const node = lowerVisualNode(
      new DropTarget(box(), () => {}, { enabled: false }) as unknown as VisualNodeValue,
      ctx,
    );

    expect(node.attributes['data-eq-drop']).toBe('off');
  });
});
