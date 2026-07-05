import { describe, expect, it } from 'vitest';
import { Button } from './__transpiled__/Button';

describe('pressed state (interaction slice 1) client lowering', () => {
  it('the transpiled Button carries the class and the cross-pinned custom property', () => {
    const node = new Button('Save', 'primary', 'medium', () => {}).render();
    expect(node.attributes.class).toBe('eq-pressable');
    // The SAME tail the C# realizer pins (Primary.Pressed light/dark pair).
    expect(node.attributes.style).toMatch(/--eq-pressed-bg: light-dark\(#[0-9a-f]{6}, #[0-9a-f]{6}\)$/);
  });

  it('disabled buttons opt out of the mechanics', () => {
    const node = new Button('Save', 'primary', 'medium', () => {}, { disabled: true }).render();
    expect(node.attributes.class).toBeUndefined();
  });
});
