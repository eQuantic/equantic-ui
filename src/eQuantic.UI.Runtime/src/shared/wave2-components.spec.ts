/** Wave-2 transpiled components: real eqc output executing against the runtime. */

import { effectiveStyle } from './style-atomizer';
import { describe, expect, it } from 'vitest';
import { Checkbox } from './__transpiled__/Checkbox';
import { IconButton } from './__transpiled__/IconButton';
import { List } from './__transpiled__/List';
import { ListItem } from './__transpiled__/ListItem';
import { Switch } from './__transpiled__/Switch';
import { Tabs } from './__transpiled__/Tabs';

describe('wave-2 transpiled components (real eqc output)', () => {
  it('Checkbox toggles through the row-level pressable', () => {
    let changed = false;
    const node = new Checkbox(false, () => {
      changed = true;
    }, 'Agree').render();
    expect(node.tag).toBe('button');
    (node.events.click as () => void)();
    expect(changed).toBe(true);
  });

  it('Switch anchors the thumb by state (on=end, off=start)', () => {
    const on = new Switch(true, () => {}).render();
    expect(effectiveStyle(on.children[0].children[1])).toContain('right: 3px');
    const off = new Switch(false, () => {}).render();
    expect(effectiveStyle(off.children[0].children[1])).toContain('left: 3px');
  });

  it('IconButton fires and renders the 40dp default', () => {
    let fired = false;
    const node = new IconButton('search', 'Search', 'standard', 'medium', () => {
      fired = true;
    }).render();
    expect(effectiveStyle(node.children[0])).toContain('width: 40px');
    (node.events.click as () => void)();
    expect(fired).toBe(true);
  });

  it('List owns its dividers between items', () => {
    const node = new List([new ListItem('A'), new ListItem('B')]).render();
    expect(node.children).toHaveLength(3);
  });

  it('Tabs selects through the cell pressable', () => {
    let picked = -1;
    const node = new Tabs(['One', 'Two'], 0, (i: number) => {
      picked = i;
    }).render();
    const secondCell = node.children[1].children[0];
    (secondCell.events.click as () => void)();
    expect(picked).toBe(1);
  });
});
