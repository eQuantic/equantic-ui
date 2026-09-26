import { describe, expect, it } from 'vitest';
import { DynamicElement } from './dynamic-element';
import { dictionary } from '../utils/dictionary';

describe('DynamicElement', () => {
  // `CustomAttributes` is a Dictionary<string, string> in C#, so transpiled code hands over the
  // runtime's dictionary, and hand-written code a plain object.
  it('renders the custom attributes a transpiled dictionary carries, in its order', () => {
    const node = new DynamicElement({
      tagName: 'a',
      className: 'link',
      customAttributes: dictionary([
        ['href', '/docs'],
        ['class', 'custom'],
      ]),
    }).render();
    expect(node.tag).toBe('a');
    expect(Object.entries(node.attributes)).toEqual([
      ['class', 'custom'],
      ['href', '/docs'],
    ]);
  });

  it('keeps an attribute named "__proto__" a dictionary holds, never the prototype', () => {
    const node = new DynamicElement({
      tagName: 'x-tag',
      customAttributes: dictionary([['__proto__', 'kept']]),
    }).render();
    expect(Object.keys(node.attributes)).toEqual(['__proto__']);
    expect(Object.getPrototypeOf(node.attributes)).toBe(Object.prototype);
  });

  it('renders custom attributes from a plain object too', () => {
    const node = new DynamicElement({ tagName: 'my-tag', customAttributes: { slot: 'end' } }).render();
    expect(node.attributes).toEqual({ slot: 'end' });
  });
});
