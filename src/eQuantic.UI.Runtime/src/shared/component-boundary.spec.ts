import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import type { ColorTokenValue, VisualNodeValue } from './nodes';
import { lowerVisualNode } from './lowering';

/**
 * The BOUNDARY: a component whose build throws costs its own subtree and nothing else.
 *
 * Before this, `build` threw straight through the lowering into the mount, nothing was ever written
 * to the root, and the page stayed white — one null reference in one card, and every other thing on
 * the screen ceased to exist.
 */

const textPrimary: ColorTokenValue = {
  light: { r: 0x17, g: 0x1b, b: 0x21, a: 255 },
  dark: { r: 0xf2, g: 0xf4, b: 0xf7, a: 255 },
};
const ctx = { textPrimary };

/** A component that fails the way real ones do — mid-build, on data it assumed was there. */
const exploding = (name = 'BrokenCard'): VisualNodeValue =>
  ({
    nodeKind: 'component',
    constructor: { name },
    build: () => {
      throw new TypeError("Cannot read properties of undefined (reading 'title')");
    },
  }) as unknown as VisualNodeValue;

const text = (content: string): VisualNodeValue =>
  ({ nodeKind: 'text', content, role: 'body', maxLines: 1 }) as unknown as VisualNodeValue;

/** Every word the DOM would show, in order — what a reader of the page actually gets. */
const flatten = (node: unknown): string => {
  const value = node as { children?: unknown[]; textContent?: string };
  const own = typeof value.textContent === 'string' ? value.textContent : '';
  const kids = (value.children ?? []).map(flatten).join(' ');
  return `${own} ${kids}`.trim();
};

describe('component boundary', () => {
  let errors: unknown[][];

  beforeEach(() => {
    errors = [];
    vi.spyOn(console, 'error').mockImplementation((...args: unknown[]) => {
      errors.push(args);
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    delete (window as { __EQ_DEV__?: boolean }).__EQ_DEV__;
  });

  it('contains the throw instead of letting it reach the mount', () => {
    expect(() => lowerVisualNode(exploding(), ctx)).not.toThrow();
  });

  it('renders the failing component as a surface, and its SIBLINGS still render', () => {
    const row = {
      nodeKind: 'row',
      gap: 8,
      main: 'start',
      cross: 'start',
      children: [text('before'), exploding(), text('after')],
    } as unknown as VisualNodeValue;

    const rendered = flatten(lowerVisualNode(row, ctx));

    expect(rendered).toContain('before');
    expect(rendered).toContain('after');
    expect(rendered).toContain('could not be displayed');
  });

  it('reports the failure — a boundary that only swallows is a silent crash', () => {
    lowerVisualNode(exploding('PriceTag'), ctx);

    expect(errors).toHaveLength(1);
    expect(String(errors[0][0])).toContain('PriceTag');
  });

  it('keeps the exception to itself in production', () => {
    const rendered = flatten(lowerVisualNode(exploding(), ctx));

    expect(rendered).toContain('This section could not be displayed.');
    // An exception message is written for whoever wrote the code — it can name an id, a path or a
    // query, and none of that belongs on a stranger's screen.
    expect(rendered).not.toContain('undefined');
    expect(rendered).not.toContain('BrokenCard');
  });

  it('names the component and quotes the throw when a developer is watching', () => {
    (window as { __EQ_DEV__?: boolean }).__EQ_DEV__ = true;

    const rendered = flatten(lowerVisualNode(exploding(), ctx));

    expect(rendered).toContain('BrokenCard failed to render');
    expect(rendered).toContain('TypeError');
  });
});
