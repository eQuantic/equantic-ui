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
import { Column, GridTrack, Text } from './vocabulary';
import { Accordion } from './components/Accordion';
import { AccordionItem } from './components/AccordionItem';
import { Badge } from './components/Badge';
import { Card } from './components/Card';
import { Checkbox } from './components/Checkbox';
import { Chip } from './components/Chip';
import { Divider } from './components/Divider';
import { Banner } from './components/Banner';
import { Stepper } from './components/Stepper';
import { Pagination } from './components/Pagination';
import { PageIndicator } from './components/PageIndicator';
import { Tooltip } from './components/Tooltip';
import { Tabs } from './components/Tabs';
import { SearchField } from './components/SearchField';
import { TextInput } from './components/TextInput';
import { RadioGroup } from './components/RadioGroup';
import { EmptyState } from './components/EmptyState';
import { Menu } from './components/Menu';
import { MenuItem } from './components/MenuItem';
import { Dialog } from './components/Dialog';
import { DialogAction } from './components/DialogAction';
import { ListItem } from './components/ListItem';
import { ListView } from './components/ListView';
import { Table } from './components/Table';
import { DataTable } from './components/DataTable';
import { DataColumn } from './components/DataColumn';
import { DataRow } from './components/DataRow';
import { Popover } from './components/Popover';
import { Drawer } from './components/Drawer';
import { Avatar } from './components/Avatar';
import { Button } from './components/Button';
import { ProgressBar } from './components/ProgressBar';
import { Select } from './components/Select';
import { Switch } from './components/Switch';
import { Calendar } from './components/Calendar';
import { dateOnly } from '../utils/datetime';
import { installCulture } from '../utils/culture';
import calendarNames from './calendar-names.fixture.json';

setPhotonTheme(photonTheme);

// The calendar reads the culture for its names, so the twin replays under the SAME one the C#
// generator fixed — with the shipped catalog, which is what a server-rendered page installs.
installCulture('en-US', 'en-US', {}, calendarNames['en-US']);

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
    badge: still(new Badge(7)),
    'badge-overflow': still(new Badge(140, 99, 'primary')),
    card: still(new Card(new Text('body', 'bodyM', photonTheme.textPrimary))),
    'checkbox-on': still(new Checkbox(true, null, 'Accept')),
    'checkbox-off': still(new Checkbox(false)),
    chip: still(new Chip('Filter')),
    'chip-selected': still(new Chip('Chosen', 'filter', true)),
    divider: still(new Divider()),
    'divider-vertical': still(new Divider('none', 'vertical')),
    banner: still(new Banner('destructive', 'Careful', 'Something needs attention')),
    stepper: still(new Stepper(3)),
    'stepper-labelled': still(Object.assign(new Stepper(3), { label: 'quantity' })),
    pagination: still(new Pagination(5, 2)),
    'page-indicator': still(new PageIndicator(4, 1)),
    tooltip: still(new Tooltip(new Text('hover', 'bodyM', photonTheme.textPrimary), 'the tip')),
    tabs: still(new Tabs(['One', 'Two', 'Three'], 1)),
    'search-field': still(new SearchField('term')),
    'text-input': still(new TextInput('value', null, 'Label')),
    'radio-group': still(new RadioGroup(['a', 'b'], 0)),
    'empty-state': still(new EmptyState('search', 'Nothing here', 'Try another term')),
    'menu-closed': still(new Menu(new Button('Open'), [new MenuItem('One'), new MenuItem('Two')])),
    dialog: still(
      new Dialog('Delete this?', 'It cannot be undone.', [
        new DialogAction('Cancel', null, 'ghost'),
        new DialogAction('Delete'),
      ]),
    ),
    'list-item': still(new ListItem('Title', 'and a subtitle')),
    'list-view': still(
      new ListView(
        3,
        48,
        (index: number) => new Text(`row ${index}`, 'bodyM', photonTheme.textPrimary),
      ),
    ),
    table: still(
      new Table(
        ['Name', 'Size'],
        [
          ['a', '1'],
          ['b', '2'],
        ],
      ),
    ),
    'data-table': still(
      new DataTable(
        [
          new DataColumn('Name', GridTrack.flex()),
          new DataColumn('Size', GridTrack.fixed(80), 'end'),
        ],
        [
          new DataRow('a', [
            new Text('alpha', 'bodyM', photonTheme.textPrimary),
            new Text('1', 'bodyM', photonTheme.textPrimary),
          ]),
        ],
      ),
    ),
    'popover-closed': still(
      new Popover(
        new Button('Info'),
        new Text('the content', 'bodyM', photonTheme.textPrimary),
        false,
      ),
    ),
    'drawer-open': still(new Drawer(new Text('side', 'bodyM', photonTheme.textPrimary), true)),
    accordion: still(
      new Accordion([
        new AccordionItem('First', new Text('one', 'bodyM', photonTheme.textPrimary)),
        new AccordionItem('Second', new Text('two', 'bodyM', photonTheme.textPrimary)),
      ]),
    ),
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
    'calendar-july-2026': still(new Calendar(dateOnly(2026, 7, 17))),
    'calendar-bounded': still(
      new Calendar(dateOnly(2026, 7, 17), null, dateOnly(2026, 7, 10), dateOnly(2026, 7, 20)),
    ),
    // Driven: index 2 is the first day CELL (the two chevrons come first in tree order).
    'calendar-picks-a-day': { node: new Calendar(dateOnly(2026, 7, 17)), presses: [2] },
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
/**
 * Handlers `lowerTextEntry` attaches to the entry ELEMENT itself, which SSR never emits: it says
 * it produces "identical DOM to the C# SSR realizer, plus the client-only handlers", because the
 * server sends markup and the client attaches behaviour. C# attaches none of these, so there is no
 * matching filter on that side — the asymmetry is the point.
 *
 * Scoped to the entry's own tag on purpose. Other components attach the same NAMES for their own
 * reasons — the spreadsheet surface listens for keydown and the clipboard trio — and filtering
 * globally would hide a real regression the day one of those joins the fixture.
 */
const CLIENT_ONLY_EVENTS = new Set(['focus', 'blur', 'input', 'keydown', 'paste', 'cut', 'copy']);
const ENTRY_TAGS = new Set(['input', 'textarea']);

function canonical(node: HtmlNode): unknown {
  const attrs: Record<string, string> = {};
  for (const key of Object.keys(node.attributes ?? {}).sort()) {
    // The channel KEY itself goes with the channel — a path the client stamps to find the element
    // again after it mounts, which the server has no reason to write.
    if (key === 'data-eq-scroll') continue;
    const value = node.attributes[key];
    if (value !== undefined && value !== null) attrs[key] = normalize(key, value);
  }
  const result: Record<string, unknown> = { tag: node.tag };
  if (node.key !== undefined && node.key !== null) result.key = node.key;
  if (node.textContent !== undefined && node.textContent !== null) result.text = node.textContent;
  result.attrs = attrs;
  const entry = ENTRY_TAGS.has(node.tag);
  // A SCROLL VIEW's after-pass channel is client-only for a reason the module states: "a windowed
  // list is (offset, viewport) and neither is knowable before layout". The server has no layout, so
  // it emits neither the channel key nor the listener that reports back through it. Recognised by
  // the key itself rather than by tag, and only there — a scroll handler anywhere else still counts.
  const scrollView = 'data-eq-scroll' in (node.attributes ?? {});
  result.events = Object.keys(node.events ?? {})
    .filter(
      (name) => !(entry && CLIENT_ONLY_EVENTS.has(name)) && !(scrollView && name === 'scroll'),
    )
    .sort();
  result.children = (node.children ?? []).map(canonical);
  return result;
}

/**
 * Cases whose trees do NOT agree yet, each for a reason worth writing down rather than papering
 * over. They still run: the assertion is inverted, so the day a case starts agreeing the suite
 * fails and the entry comes out — the list can only shrink.
 *
 * - `text-input`: the twin gives the empty helper line a text node and C# gives the span no child
 *   at all, so the client's tree has one node the server's HTML does not. That is the shape the
 *   forms work already met once — an empty SSR text the reconciler never filled — and fixing it
 *   is a hydration decision in TextInput, which is someone else's open work right now.
 */
const KNOWN_DIVERGENCES = new Set(['text-input']);

describe('component parity: the twin lowers to the tree C# lowers to', () => {
  const pinned = fixture as Record<string, unknown>;
  const built = cases();

  it('covers exactly the components the C# side pins', () => {
    expect(Object.keys(built).sort()).toEqual(Object.keys(pinned).sort());
  });

  for (const [name, { node, presses }] of Object.entries(built)) {
    const known = KNOWN_DIVERGENCES.has(name);
    it(`${name} lowers identically${presses.length > 0 ? ' through its presses' : ''}${known ? ' (known divergence)' : ''}`, () => {
      const actual = frames(node, presses);
      // A structural mismatch reads as "expected {…} to deeply equal {…}" and tells you nothing
      // about WHERE. EQ_PARITY_DIFF=1 prints both trees so the differing attribute is visible.
      if (process.env.EQ_PARITY_DIFF && JSON.stringify(actual) !== JSON.stringify(pinned[name])) {
        console.log(
          `\n=== ${name}\n--- C#\n${JSON.stringify(pinned[name], null, 1)}` +
            `\n--- twin\n${JSON.stringify(actual, null, 1)}`,
        );
      }
      if (known) {
        // Recorded rather than hidden, and the list may only SHRINK: a case in it still runs, so
        // the day it starts agreeing the test says so and the entry comes out.
        expect(
          JSON.stringify(actual),
          `${name} now AGREES — remove it from KNOWN_DIVERGENCES`,
        ).not.toEqual(JSON.stringify(pinned[name]));
        return;
      }
      expect(actual).toEqual(pinned[name]);
    });
  }
});
