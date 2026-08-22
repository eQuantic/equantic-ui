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
import { Avatar } from './components/Avatar';
import { Button } from './components/Button';
import { ProgressBar } from './components/ProgressBar';
import { Switch } from './components/Switch';

setPhotonTheme(photonTheme);

const lower = (node: unknown): HtmlNode =>
  lowerVisualNode(node as never, {
    textPrimary: photonTheme.textPrimary,
    componentContext: { theme: photonTheme, typeScale: 1 },
  });

/** The same names the C# side builds, with the same arguments. */
function cases(): Record<string, unknown> {
  const column = (gap: number, ...children: unknown[]) => {
    const node = new Column(gap);
    for (const child of children) node.add(child as never);
    return node;
  };
  return {
    text: new Text('hello', 'bodyM', photonTheme.textPrimary),
    'button-primary': new Button('Save'),
    'button-ghost-small': new Button('Cancel', 'ghost', 'small'),
    'switch-on': new Switch(true),
    'switch-off': new Switch(false),
    'progress-determinate': new ProgressBar(0.42),
    'progress-indeterminate': new ProgressBar(),
    avatar: new Avatar('EM', 'large', 'Edgar'),
    'column-of-text': column(
      12,
      new Text('one', 'bodyM', photonTheme.textPrimary),
      new Text('two', 'label', photonTheme.textPrimary),
    ),
    'button-in-column': column(8, new Button('A'), new Button('B', 'outline')),
  };
}

/** A lowered node the way the C# half writes it — the shapes have to be byte-comparable. */
function canonical(node: HtmlNode): unknown {
  const attrs: Record<string, string> = {};
  for (const key of Object.keys(node.attributes ?? {}).sort()) {
    const value = node.attributes[key];
    if (value !== undefined && value !== null) attrs[key] = value;
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

  for (const [name, node] of Object.entries(built)) {
    it(`${name} lowers identically`, () => {
      const actual = canonical(lower(node));
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
