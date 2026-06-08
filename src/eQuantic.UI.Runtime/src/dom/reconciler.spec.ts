import { describe, it, expect, beforeEach } from 'vitest';
import { Reconciler } from './reconciler';
import { HtmlNode } from '../core/types';

/**
 * Helper to create HtmlNode (virtual DOM node)
 */
function h(tag: string, key?: string | number | null, children: HtmlNode[] = []): HtmlNode {
  return {
    tag,
    key: key?.toString(),
    children,
    attributes: {},
    events: {},
  };
}

describe('Reconciler Keyed Diffing', () => {
  let reconciler: Reconciler;
  let parent: HTMLElement;

  beforeEach(() => {
    reconciler = new Reconciler();
    parent = document.createElement('div');
  });

  it('should mount children', () => {
    const oldV = h('div', 'root', []);
    reconciler.reconcile(parent, null, oldV);
    const rootEl = parent.childNodes[0] as HTMLElement;

    const newV = h('div', 'root', [h('span', 'A'), h('span', 'B')]);
    reconciler.reconcile(parent, oldV, newV, 0);

    expect(rootEl.childNodes.length).toBe(2);
    expect((rootEl.childNodes[0] as HTMLElement).tagName).toBe('SPAN');
    expect((rootEl.childNodes[1] as HTMLElement).tagName).toBe('SPAN');
  });

  it('should reorder nodes (swap)', () => {
    // Initial: A, B
    const oldV = h('div', 'root', [h('span', 'A'), h('span', 'B')]);
    reconciler.reconcile(parent, null, oldV);
    const rootEl = parent.childNodes[0] as HTMLElement;
    const [nodeA, nodeB] = Array.from(rootEl.childNodes);

    // Update: B, A
    const newV = h('div', 'root', [h('span', 'B'), h('span', 'A')]);
    reconciler.reconcile(parent, oldV, newV, 0);

    // Verify order - same instances should be moved
    expect(rootEl.childNodes.length).toBe(2);
    expect(rootEl.childNodes[0]).toBe(nodeB);
    expect(rootEl.childNodes[1]).toBe(nodeA);
  });

  it('should insert in middle', () => {
    // Initial: A, C
    const oldV = h('div', 'root', [h('span', 'A'), h('span', 'C')]);
    reconciler.reconcile(parent, null, oldV);
    const rootEl = parent.childNodes[0] as HTMLElement;
    const [nodeA, nodeC] = Array.from(rootEl.childNodes);

    // Update: A, B, C
    const newV = h('div', 'root', [h('span', 'A'), h('span', 'B'), h('span', 'C')]);
    reconciler.reconcile(parent, oldV, newV, 0);

    expect(rootEl.childNodes.length).toBe(3);
    expect(rootEl.childNodes[0]).toBe(nodeA);
    expect((rootEl.childNodes[1] as HTMLElement).tagName).toBe('SPAN'); // New B
    expect(rootEl.childNodes[2]).toBe(nodeC);
  });

  it('should remove from middle', () => {
    // Initial: A, B, C
    const oldV = h('div', 'root', [h('span', 'A'), h('span', 'B'), h('span', 'C')]);
    reconciler.reconcile(parent, null, oldV);
    const rootEl = parent.childNodes[0] as HTMLElement;
    const [nodeA, nodeB, nodeC] = Array.from(rootEl.childNodes);

    // Update: A, C
    const newV = h('div', 'root', [h('span', 'A'), h('span', 'C')]);
    reconciler.reconcile(parent, oldV, newV, 0);

    expect(rootEl.childNodes.length).toBe(2);
    expect(rootEl.childNodes[0]).toBe(nodeA);
    expect(rootEl.childNodes[1]).toBe(nodeC);
    // Node B should be detached
    expect(nodeB.parentNode).toBeNull();
  });

  it('should handle complex LIS reorder', () => {
    // Initial: A, B, C, D, E
    const oldV = h('div', 'root', [
      h('span', 'A'),
      h('span', 'B'),
      h('span', 'C'),
      h('span', 'D'),
      h('span', 'E'),
    ]);
    reconciler.reconcile(parent, null, oldV);
    const rootEl = parent.childNodes[0] as HTMLElement;
    const [a, b, c, d, e] = Array.from(rootEl.childNodes);

    // Update: A, C, E, B, D (Mix of moves)
    const newV = h('div', 'root', [
      h('span', 'A'),
      h('span', 'C'),
      h('span', 'E'),
      h('span', 'B'),
      h('span', 'D'),
    ]);
    reconciler.reconcile(parent, oldV, newV, 0);

    // Verify positions
    const newNodes = Array.from(rootEl.childNodes);
    expect(newNodes.length).toBe(5);
    expect(newNodes[0]).toBe(a);
    expect(newNodes[1]).toBe(c);
    expect(newNodes[2]).toBe(e);
    expect(newNodes[3]).toBe(b);
    expect(newNodes[4]).toBe(d);
  });

  it('should replace node when key changes', () => {
    // Initial: A, B, C
    const oldV = h('div', 'root', [h('span', 'A'), h('span', 'B'), h('span', 'C')]);
    reconciler.reconcile(parent, null, oldV);
    const rootEl = parent.childNodes[0] as HTMLElement;
    const originalThird = rootEl.childNodes[2];

    // Update: A, B, D (C replaced by D)
    const newV = h('div', 'root', [h('span', 'A'), h('span', 'B'), h('span', 'D')]);
    reconciler.reconcile(parent, oldV, newV, 0);

    expect(rootEl.childNodes.length).toBe(3);
    // Third node should be a different instance (replaced)
    expect(rootEl.childNodes[2]).not.toBe(originalThird);
  });
});

const SVG_NS = 'http://www.w3.org/2000/svg';

function el(
  tag: string,
  attributes: Record<string, string> = {},
  children: HtmlNode[] = [],
): HtmlNode {
  return { tag, children, attributes, events: {} };
}

describe('Reconciler boolean attributes', () => {
  let reconciler: Reconciler;

  beforeEach(() => {
    reconciler = new Reconciler();
  });

  it('disabled="false" must NOT disable (attribute removed)', () => {
    const btn = reconciler.createDomElement(
      el('button', { disabled: 'false' }),
    ) as HTMLButtonElement;
    expect(btn.disabled).toBe(false);
    expect(btn.hasAttribute('disabled')).toBe(false);
  });

  it('disabled="" (presence) disables', () => {
    const btn = reconciler.createDomElement(el('button', { disabled: '' })) as HTMLButtonElement;
    expect(btn.disabled).toBe(true);
  });

  it('checked="false" must NOT check the box', () => {
    const input = reconciler.createDomElement(
      el('input', { type: 'checkbox', checked: 'false' }),
    ) as HTMLInputElement;
    expect(input.checked).toBe(false);
  });

  it('checked="true" checks the box', () => {
    const input = reconciler.createDomElement(
      el('input', { type: 'checkbox', checked: 'true' }),
    ) as HTMLInputElement;
    expect(input.checked).toBe(true);
  });

  it('readonly="false" maps to readOnly=false', () => {
    const input = reconciler.createDomElement(
      el('input', { readonly: 'false' }),
    ) as HTMLInputElement;
    expect(input.readOnly).toBe(false);
  });
});

describe('Reconciler dispose', () => {
  it('detaches event listeners from the DOM on dispose', () => {
    const reconciler = new Reconciler();
    let clicks = 0;
    const node: HtmlNode = {
      tag: 'button',
      children: [],
      attributes: {},
      events: {
        click: () => {
          clicks++;
        },
      },
    };

    const btn = reconciler.createDomElement(node) as HTMLButtonElement;
    document.body.appendChild(btn);

    btn.click();
    expect(clicks).toBe(1);
    expect(reconciler.getEventListenerCount()).toBe(1);

    reconciler.dispose();
    expect(reconciler.getEventListenerCount()).toBe(0);

    btn.click();
    expect(clicks).toBe(1); // listener was removed, no further increments

    document.body.removeChild(btn);
  });
});

describe('Reconciler SVG namespace', () => {
  let reconciler: Reconciler;

  beforeEach(() => {
    reconciler = new Reconciler();
  });

  it('creates <svg> and descendants in the SVG namespace', () => {
    const svg = reconciler.createDomElement(
      el('svg', { viewBox: '0 0 24 24' }, [el('path', { d: 'M0 0' })]),
    ) as Element;
    expect(svg.namespaceURI).toBe(SVG_NS);
    expect(svg.firstChild && (svg.firstChild as Element).namespaceURI).toBe(SVG_NS);
  });

  it('preserves camelCase SVG attribute names (viewBox)', () => {
    const svg = reconciler.createDomElement(el('svg', { viewBox: '0 0 24 24' })) as Element;
    expect(svg.getAttribute('viewBox')).toBe('0 0 24 24');
    expect(svg.hasAttribute('viewbox')).toBe(false);
  });

  it('keeps plain HTML elements in the HTML namespace', () => {
    const div = reconciler.createDomElement(el('div')) as Element;
    expect(div.namespaceURI).toBe('http://www.w3.org/1999/xhtml');
  });
});

describe('Reconciler hydration', () => {
  it('aligns children when the virtual tree has a whitespace text node the DOM omitted', () => {
    const reconciler = new Reconciler();
    const container = document.createElement('div');
    container.innerHTML = '<button>Click</button>'; // SSR emitted only the button

    let clicks = 0;
    const text = (content: string): HtmlNode => ({
      tag: '#text',
      textContent: content,
      attributes: {},
      events: {},
      children: [],
    });
    const virtualNode: HtmlNode = {
      tag: 'div',
      attributes: {},
      events: {},
      children: [
        text('  '), // whitespace-only text node that the server did not render
        {
          tag: 'button',
          attributes: {},
          events: {
            click: () => {
              clicks++;
            },
          },
          children: [text('Click')],
        },
      ],
    };

    const result = reconciler.hydrate(container, virtualNode);
    expect(result.success).toBe(true);

    (container.querySelector('button') as HTMLButtonElement).click();
    expect(clicks).toBe(1);
  });
});
