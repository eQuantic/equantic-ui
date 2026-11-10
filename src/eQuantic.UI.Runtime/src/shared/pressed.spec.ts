import { CONTROL_PRESSED, effectiveStyle, stateStyle } from './style-atomizer';
import { describe, expect, it } from 'vitest';
import { Button } from './components/Button';
import { photonTheme } from './design-system.generated';
import { tokenValue } from './lowering';
import type { HtmlNode } from '../core/types';

const classes = (node: HtmlNode): string[] => (node.attributes['class'] ?? '').split(' ');

/**
 * A pressed fill is the Pressed diff of the box it fills (#616), the C# twin's one mechanism: the
 * control carries no fill of its own, no custom property and no marker, and its surface turns into
 * the fill under the pressed family's rule, which outranks the surface's hover and its focus by
 * specificity alone.
 */
describe('a pressed fill is its surface\'s Pressed diff (#616)', () => {
  it('the transpiled Button presses its surface into Primary.Pressed', () => {
    const node = new Button('Save', 'primary', 'medium', () => {}).render();
    expect(classes(node)).toContain('eq-pressable');
    expect(classes(node)).not.toContain('eq-press-fill');
    expect(effectiveStyle(node)).not.toContain('--eq-pressed-bg');

    const surface = node.children[0] as HtmlNode;
    expect(stateStyle(surface, CONTROL_PRESSED)).toBe(
      `background-color: ${tokenValue(photonTheme.colors('primary').pressed)}`,
    );
  });

  // #508's defect, which lived in the mechanism #616 removed: a control with no pressed fill went
  // transparent while pressed. A link button declares none, so its surface takes no pressed rule.
  it('a button with no pressed fill keeps its surface as it is while pressed', () => {
    const node = new Button('Docs', 'link', 'medium', () => {}).render();
    expect(stateStyle(node.children[0] as HtmlNode, CONTROL_PRESSED)).not.toContain('background');
  });

  it('a disabled button takes no pressed state at all', () => {
    const node = new Button('Save', 'primary', 'medium', () => {}, { disabled: true }).render();
    expect(node.attributes.class ?? '').not.toContain('eq-pressable');
    expect(stateStyle(node.children[0] as HtmlNode, CONTROL_PRESSED)).toBe('');
  });
});
