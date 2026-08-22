/**
 * COMPONENT PARITY — the twin's OUTPUT, not its source (docs/COVERAGE-PLAN.md slice 4).
 *
 * The transpilation pins compare the twin's SOURCE against a committed module, and the Studio
 * walks run one side. Neither asks the question that matters at run time: given the same
 * component with the same arguments, does the twin LOWER to the tree C# lowers to? A twin can be
 * transpiled perfectly and still produce different DOM — a lost event handler, an attribute the
 * realizer sets on one side only, a style class whose hash drifted.
 *
 * The fixture is written by the C# ComponentParityFixtureTests (EQ_UPDATE_PARITY_FIXTURE=1) from
 * `WebRealizer.Lower`, and replayed here through `lowerVisualNode`. Both serialise the same way:
 * tag, key, text, attributes and event NAMES sorted, children in order. Events cross as names
 * because a delegate and a closure cannot be compared — but whether the handler is still THERE is
 * exactly what goes missing.
 */

import { describe, expect, it } from 'vitest';
import fixture from './component-parity.fixture.json';
import { photonTheme } from './design-system.generated';
import { lowerVisualNode } from './lowering';
import { setPhotonTheme } from './photon-context';
import type { HtmlNode } from '../core/types';
import { Column, Text } from './vocabulary';
import { Accordion } from './components/Accordion';
import { AccordionItem } from './components/AccordionItem';
import { Avatar } from './components/Avatar';
import { Button } from './components/Button';
import { ProgressBar } from './components/ProgressBar';
import { Select } from './components/Select';
import { Switch } from './components/Switch';

setPhotonTheme(photonTheme);

const lower = (node: unknown): HtmlNode =>
  lowerVisualNode(node as never, {
    textPrimary: photonTheme.textPrimary,
    componentContext: { theme: photonTheme, typeScale: 1 },
  });

/**
 * The same names the C# side builds, with the same arguments — and the same PRESSES. A press is
 * the index of a click handler in the lowered tree, in document order, invoked between one frame
 * and the next: the index means the same control on both sides because both walk the tree the
 * same way. A component with no presses is one frame.
 */
function cases(): Record<string, { node: unknown; presses: number[] }> {
  const column = (gap: number, ...children: unknown[]) => {
    const node = new Column(gap);
    for (const child of children) node.add(child as never);
    return node;
  };
  const still = (node: unknown) => ({ node, presses: [] });
  return {
    text: still(new Text('hello', 'bodyM', photonTheme.textPrimary)),
    'button-primary': still(new Button('Save')),
    'button-ghost-small': still(new Button('Cancel', 'ghost', 'small')),
    'switch-on': still(new Switch(true)),
    'switch-off': still(new Switch(false)),
    'progress-determinate': still(new ProgressBar(0.42)),
    'progress-indeterminate': still(new ProgressBar()),
    avatar: still(new Avatar('EM', 'large', 'Edgar')),
    'column-of-text': still(
      column(
        12,
        new Text('one', 'bodyM', photonTheme.textPrimary),
        new Text('two', 'label', photonTheme.textPrimary),
      ),
    ),
    'button-in-column': still(column(8, new Button('A'), new Button('B', 'outline'))),
    'select-opens': { node: new Select(['alpha', 'beta', 'gamma'], 0), presses: [0] },
    'accordion-switches': {
      node: new Accordion(
        [
          new AccordionItem('One', new Text('body one', 'bodyM', photonTheme.textPrimary)),
          new AccordionItem('Two', new Text('body two', 'bodyM', photonTheme.textPrimary)),
        ],
        0,
      ),
      presses: [1],
    },
  };
}

/**
 * A generated ID reduced to its shape. An anchored panel's id is a HASH, and the two sides hash
 * different inputs — safe today only because an open panel never comes from SSR, so the two never
 * meet in one document. Comparing the hashes would fail on a difference the product allows; not
 * comparing them would hide a reference pointing at nothing, which `referencesResolve` checks
 * separately on the un-normalised tree.
 */
function normalize(attribute: string, value: string): string {
  // CLASS is compared as a SET: attribute order does not enter the CSS cascade — the stylesheet's
  // does — and the two sides do build the list in a different order. WHICH classes are there is
  // what carries meaning, since each is a hash of a declaration, and that stays pinned.
  const normalised =
    attribute === 'class' ? value.split(' ').filter(Boolean).sort().join(' ') : value;
  return normalised.replace(/eq-panel-[a-z0-9]+/g, 'eq-panel-#');
}

/**
 * Every ARIA reference in the tree names an id the tree HAS. A dangling `aria-activedescendant`
 * reads to a screen reader as no focus at all, and pinning the attribute against itself cannot see
 * it — which is how one survived having a test.
 */
function referencesResolve(root: HtmlNode): void {
  const ids = new Set<string>();
  const references: [string, string][] = [];
  const walk = (node: HtmlNode) => {
    const id = node.attributes?.id;
    if (id) ids.add(id);
    for (const name of [
      'aria-activedescendant',
      'aria-controls',
      'aria-labelledby',
      'aria-describedby',
      'aria-owns',
    ]) {
      const value = node.attributes?.[name];
      if (value) references.push([name, value]);
    }
    for (const child of node.children ?? []) walk(child);
  };
  walk(root);
  for (const [attribute, value] of references)
    for (const target of value.split(' ').filter(Boolean))
      expect(ids, `${attribute} must name an element the tree has`).toContain(target);
}

/** Every click handler in the tree, in DOCUMENT order — the order the C# side walks. */
function clickHandlers(node: HtmlNode): (() => void)[] {
  const here = Object.entries(node.events ?? {})
    .filter(([name]) => name === 'click' || name === 'onclick')
    .map(([, handler]) => handler as () => void);
  return [...here, ...(node.children ?? []).flatMap(clickHandlers)];
}

/** The lowering, then the lowering after each press. */
function frames(node: unknown, presses: number[]): unknown[] {
  let frame = lower(node);
  referencesResolve(frame);
  const out: unknown[] = [canonical(frame)];
  for (const index of presses) {
    const handlers = clickHandlers(frame);
    expect(handlers.length).toBeGreaterThan(index);
    handlers[index]();
    frame = lower(node);
    referencesResolve(frame);
    out.push(canonical(frame));
  }
  return out;
}

/** A lowered node the way the C# half writes it — the shapes have to be byte-comparable. */
function canonical(node: HtmlNode): unknown {
  const attrs: Record<string, string> = {};
  for (const key of Object.keys(node.attributes ?? {}).sort()) {
    const value = node.attributes[key];
    if (value !== undefined && value !== null) attrs[key] = normalize(key, value);
  }
  const result: Record<string, unknown> = { tag: node.tag };
  if (node.key !== undefined && node.key !== null) result.key = node.key;
  if (node.textContent !== undefined && node.textContent !== null) result.text = node.textContent;
  result.attrs = attrs;
  result.events = Object.keys(node.events ?? {}).sort();
  result.children = (node.children ?? []).map(canonical);
  return result;
}

describe('component parity: the twin lowers to the tree C# lowers to', () => {
  const pinned = fixture as Record<string, unknown>;
  const built = cases();

  it('covers exactly the components the C# side pins', () => {
    expect(Object.keys(built).sort()).toEqual(Object.keys(pinned).sort());
  });

  for (const [name, { node, presses }] of Object.entries(built)) {
    it(`${name} lowers identically${presses.length > 0 ? ' through its presses' : ''}`, () => {
      const actual = frames(node, presses);
      // A structural mismatch reads as "expected {…} to deeply equal {…}" and tells you nothing
      // about WHERE. EQ_PARITY_DIFF=1 prints both trees so the differing attribute is visible.
      if (process.env.EQ_PARITY_DIFF && JSON.stringify(actual) !== JSON.stringify(pinned[name])) {
        console.log(
          `\n=== ${name}\n--- C#\n${JSON.stringify(pinned[name], null, 1)}` +
            `\n--- twin\n${JSON.stringify(actual, null, 1)}`,
        );
      }
      expect(actual).toEqual(pinned[name]);
    });
  }
});
