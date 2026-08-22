import { describe, it, expect } from 'vitest';
import { lowerVisualNode } from './lowering';
import { photonTheme } from './design-system.generated';
import { effectiveStyle } from './style-atomizer';
import type { LoweringContext } from './lowering';
import type { HtmlNode } from '../core/types';
import type { VisualNodeValue } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };

const text = (content: string): VisualNodeValue =>
  ({ nodeKind: 'text', content, role: 'label' }) as unknown as VisualNodeValue;

const cell = (day: string, selected: boolean): VisualNodeValue =>
  ({
    nodeKind: 'pressable',
    child: text(day),
    role: 'gridCell',
    selected,
    label: `July ${day}`,
    onPressed: () => {},
  }) as unknown as VisualNodeValue;

const row = (...children: VisualNodeValue[]): VisualNodeValue =>
  ({ nodeKind: 'row', gap: 0, main: 'start', cross: 'center', children }) as unknown as VisualNodeValue;

function month(onMove?: (move: string) => void, label = 'July 2026'): VisualNodeValue {
  return {
    nodeKind: 'navigable',
    rows: [row(text('S'), text('M')), row(cell('1', false), cell('2', true))],
    onMove,
    label,
    hasHeaderRow: true,
    activeCell: [1, 1],
  } as unknown as VisualNodeValue;
}

function walk(node: HtmlNode): HtmlNode[] {
  return [node, ...(node.children ?? []).flatMap((child) => walk(child as HtmlNode))];
}

const render = (node: VisualNodeValue): HtmlNode => lowerVisualNode(node, ctx) as HtmlNode;

/**
 * The GRID vocabulary, client half (C# cross-pin: GridSemanticsTests). Same three facts the C#
 * side asserts — one tab stop, cells that state selection and rove, rows that exist for assistive
 * tech and not for layout — plus the one thing only the client has: the KEYBOARD.
 */
describe('grid semantics (C# GridSemanticsTests cross-pin)', () => {
  it('the host is the one tab stop and carries the grid identity', () => {
    const host = render(month());
    expect(host.attributes?.role).toBe('grid');
    expect(host.attributes?.tabindex).toBe('0');
    expect(host.attributes?.['aria-label']).toBe('July 2026');
    const active = host.attributes?.['aria-activedescendant'] ?? '';
    expect(active.startsWith('eq-cell-') && active.endsWith('-1-1')).toBe(true);
    // …and the reference RESOLVES. A dangling activedescendant reads to assistive tech as no focus
    // at all, and the attribute alone cannot tell you the difference.
    const target = walk(host).filter((n) => n.attributes?.id === active);
    expect(target).toHaveLength(1);
    expect(target[0].attributes?.role).toBe('gridcell');
  });

  it('two grids on one page do not share cell ids', () => {
    // The ids are DOM-global: an unscoped eq-cell-1-1 would appear twice and an activedescendant
    // could resolve into the other calendar.
    const july = walk(render(month(undefined, 'July 2026')))
      .map((n) => n.attributes?.id)
      .filter((id): id is string => id !== undefined);
    const august = walk(render(month(undefined, 'August 2026')))
      .map((n) => n.attributes?.id)
      .filter((id): id is string => id !== undefined);

    expect(july).toHaveLength(2);
    expect(august).toHaveLength(2);
    expect(july.some((id) => august.includes(id))).toBe(false);
  });

  it('the header row names its columns', () => {
    const headers = walk(render(month())).filter((n) => n.attributes?.role === 'columnheader');
    // C15: the day names ARE the column headers, so a cell announces "Friday, July 17".
    expect(headers).toHaveLength(2);
    // …and a header is never also a target the arrows can land on.
    expect(headers.every((h) => h.attributes?.id === undefined)).toBe(true);
  });

  it('cells are gridcells that state selection and leave the tab order', () => {
    const cells = walk(render(month())).filter((n) => n.attributes?.role === 'gridcell');
    expect(cells).toHaveLength(2);
    expect(cells.map((c) => c.attributes?.['aria-selected'])).toEqual(['false', 'true']);
    expect(cells.every((c) => c.attributes?.tabindex === '-1')).toBe(true);
    expect(cells[1].attributes?.['aria-label']).toBe('July 2');
  });

  it('every row is a row, and is transparent to layout', () => {
    const rows = walk(render(month())).filter((n) => n.attributes?.role === 'row');
    expect(rows).toHaveLength(2);
    // The style rides an atomic CLASS on this side (S2), so ask the atomizer what it resolves to.
    expect(
      rows.every((r) =>
        effectiveStyle(r as { attributes: Record<string, string | undefined> })
          .replace(/\s/g, '')
          .includes('display:contents'),
      ),
    ).toBe(true);
  });

  it('reads the C15 keyboard: arrows, pages, sections, row bounds', () => {
    const moves: string[] = [];
    const host = render(month((move) => moves.push(move)));
    const keydown = host.events?.keydown as unknown as (event: KeyboardEvent) => void;

    const press = (key: string, shiftKey = false) => {
      let prevented = false;
      keydown({ key, shiftKey, preventDefault: () => (prevented = true) } as unknown as KeyboardEvent);
      return prevented;
    };

    expect(press('ArrowLeft')).toBe(true);
    press('ArrowRight');
    press('ArrowUp');
    press('ArrowDown');
    press('PageUp');
    press('PageDown');
    press('PageUp', true);
    press('PageDown', true);
    press('Home');
    press('End');

    expect(moves).toEqual([
      'previousItem',
      'nextItem',
      'previousRow',
      'nextRow',
      'previousPage',
      'nextPage',
      'previousSection',
      'nextSection',
      'rowStart',
      'rowEnd',
    ]);
  });

  it('a key the grid does not claim reaches the page untouched', () => {
    const moves: string[] = [];
    const host = render(month((move) => moves.push(move)));
    const keydown = host.events?.keydown as unknown as (event: KeyboardEvent) => void;

    let prevented = false;
    keydown({
      key: 'Tab',
      shiftKey: false,
      preventDefault: () => (prevented = true),
    } as unknown as KeyboardEvent);

    // Tab must still leave the composite, and 'k' must still reach a page-level ⌘K binding.
    expect(prevented).toBe(false);
    expect(moves).toEqual([]);
  });

  it('is FOCUS-SCOPED: two grids on one page do not answer the same arrow', () => {
    const first: string[] = [];
    const second: string[] = [];
    const a = render(month((m) => first.push(m)));
    render(month((m) => second.push(m)));

    (a.events?.keydown as unknown as (e: KeyboardEvent) => void)({
      key: 'ArrowDown',
      shiftKey: false,
      preventDefault: () => {},
    } as unknown as KeyboardEvent);

    // The handler lives on the host element, so only the grid the event reached moved. This is
    // the line between Navigable and Shortcut, which is page-level by design.
    expect(first).toEqual(['nextRow']);
    expect(second).toEqual([]);
  });
});
