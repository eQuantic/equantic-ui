/**
 * A grouped Select in the browser (#677, REAL eqc output): the options of every group are one list.
 * Driven as a person drives it: a click opens the field, keydown moves the highlight, Enter chooses.
 * The C# twin is KeyboardTests.ArrowsCrossAGroup_AndEnterReportsTheIndexAcrossTheGroups.
 */

import { afterEach, describe, expect, it } from 'vitest';
import { StatefulComponent } from '../core/component';
import { resetShortcuts } from '../dom/shortcuts';
import { photonTheme } from './design-system.generated';
import { setPhotonTheme } from './photon-context';
import { Column } from './vocabulary';
import { lowerVisualNode } from './lowering';
import { Select } from './components/Select';
import { OptionGroup } from './components/OptionGroup';

setPhotonTheme(photonTheme);

const nextFrame = () =>
  new Promise<void>((resolve) =>
    requestAnimationFrame(() => requestAnimationFrame(() => resolve())),
  );

const chosen: number[] = [];

class GroupedHost extends StatefulComponent {
  build(): Column {
    const column = new Column(0);
    column.add(
      new Select(
        null,
        1,
        (index: number) => chosen.push(index),
        null,
        [new OptionGroup('Open', ['Lisboa', 'Porto']), new OptionGroup('Next', ['Braga'])],
      ) as never,
    );
    return column as never;
  }
}

describe('a grouped Select, driven in the browser', () => {
  afterEach(() => {
    resetShortcuts();
    document.body.innerHTML = '';
    chosen.length = 0;
  });

  it('ArrowDown crosses from the last option of a group to the first of the next, and Enter reports it', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);
    new GroupedHost().mount(container);

    const combobox = () => container.querySelector('[role="combobox"]') as HTMLElement;
    combobox().click();
    await nextFrame();

    const before = container.querySelector(`#${combobox().getAttribute('aria-activedescendant')}`)!;
    expect(before.textContent).toBe('Porto');

    window.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true }),
    );
    await nextFrame();

    const active = container.querySelector(`#${combobox().getAttribute('aria-activedescendant')}`)!;
    expect(active.getAttribute('role')).toBe('option');
    expect(active.textContent).toBe('Braga');
    const group = active.parentElement!.closest('[role="group"]')!;
    expect(group.getAttribute('aria-label')).toBe('Next');
    expect(group.contains(active)).toBe(true);

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }));
    await nextFrame();

    expect(chosen).toEqual([2]);
    expect(container.querySelector('[role="listbox"]')).toBeNull();
  });
});

describe('a text drawn and never read (C# twin: GroupSemanticsTests)', () => {
  const lower = (node: unknown) =>
    lowerVisualNode(node as never, { textPrimary: photonTheme.textPrimary }) as { attributes: Record<string, string> };

  it('is hidden from readers, unless a run of it links somewhere', () => {
    expect(lower({ nodeKind: 'text', content: 'Next', role: 'caption', excludeFromSemantics: true }).attributes['aria-hidden']).toBe('true');
    const linked = lower({
      nodeKind: 'text',
      content: '',
      role: 'bodyM',
      excludeFromSemantics: true,
      spans: [{ content: 'See ' }, { content: 'terms', destination: '/terms' }],
    });
    expect(linked.attributes['aria-hidden']).toBeUndefined();
  });
});
