import { afterEach, describe, expect, it } from 'vitest';
import type { ColorTokenValue, VisualNodeValue } from './nodes';
import { lowerVisualNode } from './lowering';
import { InheritedComponent, Text } from './vocabulary';
import { ComponentContext } from './photon-context';
import { photonTheme } from './design-system.generated';
import { Navigator, setNavigationHandler } from '../router/navigator';

/**
 * The browser's half of the C# `InheritedComponent` (E5, #1095), before the browser builds on the
 * element engine (E3b) and a context has a position to look one up from (E5b, #1395).
 */

const textPrimary: ColorTokenValue = {
  light: { r: 0x17, g: 0x1b, b: 0x21, a: 255 },
  dark: { r: 0xf2, g: 0xf4, b: 0xf7, a: 255 },
};

class Brand extends InheritedComponent {
  constructor(
    readonly name: string,
    child: Text,
  ) {
    super(child);
  }

  protected updateShouldNotify(old: InheritedComponent): boolean {
    return (old as Brand).name !== this.name;
  }
}

const words = (node: unknown): string => {
  const value = node as { children?: unknown[]; textContent?: string };
  const own = typeof value.textContent === 'string' ? value.textContent : '';
  return `${own} ${(value.children ?? []).map(words).join(' ')}`.trim();
};

describe('an inherited component in the browser', () => {
  afterEach(() => setNavigationHandler(null));

  it('is drawn as its child alone, as every C# pass draws it', () => {
    const bare = lowerVisualNode(new Text('inside', 'bodyM') as unknown as VisualNodeValue, { textPrimary });
    const held = lowerVisualNode(new Brand('acme', new Text('inside', 'bodyM')) as unknown as VisualNodeValue, {
      textPrimary,
    });

    expect(new Brand('acme', new Text('x', 'bodyM')).nodeKind).toBe('inherited');
    expect(words(held)).toBe(words(bare));
    expect(held).toEqual(bare);
  });

  it('is found by no context yet, so DependOn and Find answer null as C# does on a context with no position', () => {
    const context = new ComponentContext(photonTheme);

    expect(context.dependOn()).toBeNull();
    expect(context.find()).toBeNull();
  });

  it('navigates through Navigator.of(context), where Navigator.go goes', () => {
    const went: string[] = [];
    setNavigationHandler((destination) => went.push(destination));

    Navigator.of(new ComponentContext(photonTheme)).go('/settings');

    expect(went).toEqual(['/settings']);
  });
});
