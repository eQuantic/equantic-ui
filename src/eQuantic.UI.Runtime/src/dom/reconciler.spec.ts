import { describe, it, expect, beforeEach } from 'vitest';
import { Reconciler, UNMEASURED_MARK } from './reconciler';
import { commitShortcuts, declareShortcut, resetShortcuts } from './shortcuts';
import { RenderManager } from './renderer';
import { EventHandler, HtmlNode } from '../core/types';

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

  /**
   * An EMPTY text node writes nothing into HTML, so SSR of a blank caption produces
   * `<span></span>` — zero children — while the client tree holds a `#text ''`. The patch then
   * addressed a DOM node that was not there and returned in silence: a form field with no helper
   * text NEVER showed its error, on the web only, for as long as the page lived.
   */
  it('fills a node the server left empty, because empty text renders as no node at all', () => {
    const reconciler = new Reconciler();
    const container = document.createElement('div');
    container.innerHTML = '<span class="caption"></span>'; // SSR of a caption with nothing to say

    const caption = (content: string): HtmlNode => ({
      tag: 'span',
      attributes: { class: 'caption' },
      events: {},
      children: [{ tag: '#text', textContent: content, attributes: {}, events: {}, children: [] }],
    });

    reconciler.reconcile(container, caption(''), caption('The two values do not match.'));

    expect(container.querySelector('.caption')?.textContent).toBe('The two values do not match.');
  });
});

/**
 * What the server could not MEASURE, the client draws. The server has no font, so a component whose
 * geometry is text geometry is built on zeros there, and the C# realizer marks it. Everything else
 * is adopted as it always was: the mark costs the page one subtree, where a failed adoption cost it
 * the whole tree.
 */
describe('hydration of a subtree the server could not measure', () => {
  const node = (
    tag: string,
    attributes: Record<string, string>,
    children: HtmlNode[] = [],
    events: HtmlNode['events'] = {},
  ): HtmlNode => ({ tag, attributes, events, children });
  const text = (content: string): HtmlNode => ({
    tag: '#text',
    textContent: content,
    attributes: {},
    events: {},
    children: [],
  });

  /** The client's page: a heading, a code block whose gutter it measured, and a line after it. */
  const page = (gutter: string, pressed: () => void = () => {}, title = 'Ledger'): HtmlNode =>
    node('div', { class: 'page' }, [
      node('h1', { class: 'title' }, [text(title)]),
      node('div', { class: 'block' }, [
        node('div', { class: 'gutter', style: `width:${gutter}` }, [text('1')], { click: pressed }),
      ]),
      node('p', { class: 'after' }, [text('kept')]),
    ]);

  /** The server's page: the same, with the block built on a width of 0 and marked for it. */
  const served = (container: HTMLElement) => {
    container.innerHTML =
      '<div class="page"><h1 class="title">Ledger</h1>' +
      `<div class="block" ${UNMEASURED_MARK}=""><div class="gutter" style="width:12px">1</div></div>` +
      '<p class="after">kept</p></div>';
  };

  it('draws it instead of adopting the zeros it was built on, and adopts everything else', () => {
    const container = document.createElement('div');
    served(container);
    const title = container.querySelector('.title');
    const after = container.querySelector('.after');
    const draft = container.querySelector('.block');
    let presses = 0;

    const result = new Reconciler().hydrateRoot(container, page('44px', () => presses++));

    expect(result.success).toBe(true);
    // The listeners the drawn subtree carries count as attached, as the adopted ones do.
    expect(result.attachedListeners).toBe(1);
    expect(container.querySelector('.block')).not.toBe(draft);
    expect(container.querySelector('.block')?.hasAttribute(UNMEASURED_MARK)).toBe(false);
    expect((container.querySelector('.gutter') as HTMLElement).style.width).toBe('44px');
    (container.querySelector('.gutter') as HTMLElement).click();
    expect(presses).toBe(1);
    // The rest of the page is the server's own elements, adopted.
    expect(container.querySelector('.title')).toBe(title);
    expect(container.querySelector('.after')).toBe(after);
  });

  it('draws it even where the draft does not agree with the client on its shape', () => {
    const container = document.createElement('div');
    // A draft with a different tag and a child fewer: a subtree the client replaces is not one it
    // has to agree with, so this is not a failed adoption and the page is not drawn again whole.
    container.innerHTML = `<div class="page"><span ${UNMEASURED_MARK}=""></span></div>`;
    const root = container.firstElementChild;

    const result = new Reconciler().hydrateRoot(
      container,
      node('div', { class: 'page' }, [node('div', { class: 'block' }, [text('drawn')])]),
    );

    expect(result.success).toBe(true);
    expect(container.firstElementChild).toBe(root);
    expect(container.querySelector('.block')?.textContent).toBe('drawn');
  });

  /**
   * The bug itself: every update after hydration diffs the client's tree against the client's
   * tree, so a gutter still holding the server's 12px is never corrected by one that changes
   * something else. Drawn at hydration, it is the client's from the start.
   */
  it('holds the client geometry through updates that change something else', () => {
    const container = document.createElement('div');
    served(container);
    const renderer = new RenderManager();

    renderer.hydrate(page('44px'), container);
    renderer.update(page('44px', () => {}, 'Ledger, edited'));

    expect(container.querySelector('.title')?.textContent).toBe('Ledger, edited');
    expect((container.querySelector('.gutter') as HTMLElement).style.width).toBe('44px');
  });
});

describe('Reconciler event contracts', () => {
  it('a submit handler owns submission: default prevented, handler invoked with no args', () => {
    // OnSubmit is C#'s hand on the form (validate, call a server action) — the browser's own
    // navigate-away submission never runs. A form wanting native submission has no handler.
    const reconciler = new Reconciler();
    const parent = document.createElement('div');
    document.body.appendChild(parent);

    let submits = 0;
    const form: HtmlNode = {
      tag: 'form',
      attributes: {},
      events: {
        submit: (() => {
          submits++;
        }) as unknown as import('../core/types').EventHandler,
      },
      children: [],
    };
    reconciler.reconcile(parent, null, form);

    const event = new Event('submit', { bubbles: true, cancelable: true });
    (parent.querySelector('form') as HTMLFormElement).dispatchEvent(event);

    expect(submits).toBe(1);
    expect(event.defaultPrevented).toBe(true);

    parent.remove();
  });

  it('a click handler keeps the browser defaults (a checkbox still toggles)', () => {
    const reconciler = new Reconciler();
    const parent = document.createElement('div');
    document.body.appendChild(parent);

    let clicks = 0;
    const box: HtmlNode = {
      tag: 'input',
      attributes: { type: 'checkbox' },
      events: {
        click: (() => {
          clicks++;
        }) as unknown as import('../core/types').EventHandler,
      },
      children: [],
    };
    reconciler.reconcile(parent, null, box);

    const input = parent.querySelector('input') as HTMLInputElement;
    input.click();

    expect(clicks).toBe(1);
    expect(input.checked).toBe(true);

    parent.remove();
  });
});

/**
 * A key a Shortcut took reaches nothing else, which is what Photon does (PhotonHost.KeyDown asks
 * the shortcuts first and stops there). The window's shortcut listener runs in the capture phase
 * and the element's own keydown ran after it anyway: Escape closing the code editor's find bar also
 * reached the editor, which released its Tab, so the next Tab left the editor instead of indenting.
 */
describe('a key a shortcut took', () => {
  it("reaches no element's own keydown, and every other key still does", () => {
    resetShortcuts();
    let closed = 0;
    declareShortcut({ chord: 'escape', handler: () => closed++ });
    commitShortcuts();
    const seen: string[] = [];
    const parent = document.createElement('div');
    document.body.appendChild(parent);
    const input: HtmlNode = {
      tag: 'textarea',
      attributes: {},
      children: [],
      events: { keydown: ((e: KeyboardEvent) => seen.push(e.key)) as unknown as EventHandler },
    };
    new Reconciler().reconcile(parent, null, input);
    const element = parent.firstElementChild!;

    element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
    element.dispatchEvent(new KeyboardEvent('keydown', { key: 'a', bubbles: true, cancelable: true }));

    expect(closed).toBe(1);
    expect(seen).toEqual(['a']);
    parent.remove();
    resetShortcuts();
  });
});
