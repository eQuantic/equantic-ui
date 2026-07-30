import { effectiveStyle } from './style-atomizer';
import { describe, expect, it } from 'vitest';
import { ScrollView, Text } from './vocabulary';

describe('ScrollView (spec A6) client lowering', () => {
  it('vertical: overflow auto on the axis, hidden cross (the C# cross-pin)', () => {
    const node = new ScrollView(new Text('content'), 'vertical', { height: 100 }).render();
    expect(effectiveStyle(node)).toContain('height: 100px');
    expect(effectiveStyle(node)).toContain('overflow-x: hidden; overflow-y: auto');
  });

  it('horizontal flips the axes', () => {
    const node = new ScrollView(new Text('row'), 'horizontal').render();
    expect(effectiveStyle(node)).toContain('overflow-x: auto; overflow-y: hidden');
  });
});
