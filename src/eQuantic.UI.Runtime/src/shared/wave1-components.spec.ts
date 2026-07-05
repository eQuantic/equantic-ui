/** Wave-1 write-once components: real eqc output executing against the runtime (spec pins). */

import { describe, expect, it } from 'vitest';
import { Badge } from './__transpiled__/Badge';
import { Chip } from './__transpiled__/Chip';
import { Divider } from './__transpiled__/Divider';
import { ProgressBar } from './__transpiled__/ProgressBar';

describe('wave-1 transpiled components (real eqc output)', () => {
  it('Badge clamps past max and uses the Destructive pair', () => {
    const node = new Badge(120).render();
    expect(node.attributes.style).toContain('height: 16px');
    expect(node.attributes.style).toContain('background-color: light-dark(#b42318, #e5645c)');
    expect(node.children[0].children[0].children[0].textContent).toBe('99+');
  });

  it('Divider renders the 1dp Border hairline', () => {
    const node = new Divider().render();
    expect(node.attributes.style).toContain('height: 1px');
    expect(node.attributes.style).toContain('background-color: light-dark(#e2e5ea, #2a323d)');
  });

  it('ProgressBar splits the track by flex weights (640/360 at 64%)', () => {
    const node = new ProgressBar(0.64).render();
    expect(node.children[0].attributes.style).toContain('flex: 640 1 0%');
    expect(node.children[1].attributes.style).toContain('flex: 360 1 0%');
  });

  it('Filter chip toggles through a lowered button and fires onPressed', () => {
    let toggled = false;
    const node = new Chip('Income', 'filter', false, () => {
      toggled = true;
    }).render();
    expect(node.tag).toBe('button');
    (node.events.click as () => void)();
    expect(toggled).toBe(true);
  });
});
