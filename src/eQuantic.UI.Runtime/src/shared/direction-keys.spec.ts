/**
 * The horizontal arrows step in the reading direction (#1569), driven as the browser drives them: a real
 * `keydown` at the element the catalog's own `Tabs`, `RadioGroup`, `SegmentedControl`, `Slider`, `Calendar` and
 * `TreeView` lower to, under a `Directionality`. The handler reads the direction at the key, from the nearest `[dir]`
 * of the element it was attached to (`reading-direction.ts`), so what a `Directionality` writes, a `dir="ltr"` fence
 * inside an rtl page and a culture on `<html>` all answer by the one rule. ArrowUp and ArrowDown mean what they
 * always meant.
 */
import { afterEach, describe, expect, it } from 'vitest';
import { StatefulComponent } from '../core/component';
import { resetShortcuts } from '../dom/shortcuts';
import { dateOnly } from '../utils/datetime';
import { Calendar } from './components/Calendar';
import { RadioGroup } from './components/RadioGroup';
import { SegmentedControl } from './components/SegmentedControl';
import { Slider } from './components/Slider';
import { Tabs } from './components/Tabs';
import { TreeNode } from './components/TreeNode';
import { TreeView } from './components/TreeView';
import { photonTheme } from './design-system.generated';
import type { TextDirectionValue } from './enums.generated';
import { setPhotonTheme } from './photon-context';
import { Directionality } from './vocabulary';

setPhotonTheme(photonTheme);

const nextFrame = () =>
  new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));

/** One control under a `Directionality`, which writes `dir` on the element the control lowers to. */
class Under extends StatefulComponent {
  constructor(
    private readonly direction: TextDirectionValue,
    private readonly make: () => unknown,
  ) {
    super();
  }

  build(): never {
    return new Directionality(this.direction, this.make() as never) as never;
  }
}

function mount(direction: TextDirectionValue, make: () => unknown): HTMLElement {
  const container = document.createElement('div');
  document.body.appendChild(container);
  new Under(direction, make).mount(container);
  return container;
}

const key = (target: Element, name: string): KeyboardEvent => {
  const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true });
  target.dispatchEvent(event);
  return event;
};

const reading = {
  rtl: 'rightToLeft',
  ltr: 'leftToRight',
} as const satisfies Record<string, TextDirectionValue>;

afterEach(() => {
  resetShortcuts();
  document.documentElement.removeAttribute('dir');
  document.body.innerHTML = '';
});

/**
 * The same four keys, three places to read the direction from. `step` is what the control reports for the
 * keys: an Adjustable's `+1` and `-1` are "toward the end" and "toward the start", so ArrowLeft is `+1` where the
 * element reads right to left.
 */
function steps(role: string, make: (seen: number[]) => unknown, initial: number): void {
  const press = (container: HTMLElement, name: string): void => {
    key(container.querySelector(`[role="${role}"]`) as HTMLElement, name);
  };

  it('steps toward the end on ArrowLeft and toward the start on ArrowRight under rtl', () => {
    const seen: number[] = [];
    const container = mount(reading.rtl, () => make(seen));

    press(container, 'ArrowLeft');
    press(container, 'ArrowRight');

    expect(seen).toEqual([initial + 1, initial - 1]);
  });

  it('steps as in English under ltr, and inside a dir="ltr" surface of an rtl page', () => {
    const english: number[] = [];
    press(mount(reading.ltr, () => make(english)), 'ArrowRight');
    expect(english).toEqual([initial + 1]);

    document.body.innerHTML = '';
    document.documentElement.setAttribute('dir', 'rtl');
    const fenced: number[] = [];
    const container = mount(reading.ltr, () => make(fenced));
    press(container, 'ArrowRight');
    press(container, 'ArrowLeft');
    expect(fenced).toEqual([initial + 1, initial - 1]);
  });

  it('reads the direction at the key, from where the element sits then', () => {
    const seen: number[] = [];
    const container = mount(reading.ltr, () => make(seen));
    // A Directionality that changed under a kept position rewrote one attribute and nothing else.
    (container.querySelector(`[role="${role}"]`)!.closest('[dir]') as HTMLElement).setAttribute('dir', 'rtl');

    press(container, 'ArrowLeft');

    expect(seen).toEqual([initial + 1]);
  });
}

describe('Tabs (#1569)', () => {
  const tabs = (seen: number[]) => new Tabs(['One', 'Two', 'Three'], 1, (index: number) => seen.push(index));

  // Selected is the middle tab, so the next is the third and the previous the first.
  describe('by the arrows along the strip', () => {
    const press = (container: HTMLElement, name: string) => key(container.querySelector('[role="tablist"]')!, name);

    it('selects the next tab on ArrowLeft under rtl, and the previous on ArrowRight', () => {
      const seen: number[] = [];
      const container = mount(reading.rtl, () => tabs(seen));

      press(container, 'ArrowLeft');
      press(container, 'ArrowRight');

      expect(seen).toEqual([2, 0]);
    });

    it('selects the next tab on ArrowRight under ltr, and inside a dir="ltr" surface of an rtl page', () => {
      const english: number[] = [];
      press(mount(reading.ltr, () => tabs(english)), 'ArrowRight');
      expect(english).toEqual([2]);

      document.body.innerHTML = '';
      document.documentElement.setAttribute('dir', 'rtl');
      const fenced: number[] = [];
      const container = mount(reading.ltr, () => tabs(fenced));
      press(container, 'ArrowRight');
      press(container, 'ArrowLeft');
      expect(fenced).toEqual([2, 0]);
    });

    it('leaves ArrowDown as the next tab and ArrowUp as the previous in both directions', () => {
      for (const direction of [reading.rtl, reading.ltr]) {
        document.body.innerHTML = '';
        const seen: number[] = [];
        const container = mount(direction, () => tabs(seen));
        press(container, 'ArrowDown');
        press(container, 'ArrowUp');
        expect(seen, direction).toEqual([2, 0]);
      }
    });
  });
});

describe('RadioGroup (#1569)', () => {
  steps('radiogroup', (seen) => new RadioGroup(['a', 'b', 'c'], 1, (index: number) => seen.push(index)), 1);
});

describe('SegmentedControl (#1569)', () => {
  steps('radiogroup', (seen) => new SegmentedControl(['a', 'b', 'c'], 1, (index: number) => seen.push(index)), 1);
});

describe('Slider (#1569)', () => {
  // A value of 0.5 over 0..1 steps by a tenth: a step toward the end is 0.6.
  const press = (container: HTMLElement, name: string) => key(container.querySelector('[role="slider"]')!, name);

  it('rises on ArrowLeft and falls on ArrowRight under rtl', () => {
    const seen: number[] = [];
    const container = mount(reading.rtl, () => new Slider(0.5, (value: number) => seen.push(value)));

    press(container, 'ArrowLeft');
    press(container, 'ArrowRight');

    expect(seen.map((value) => Math.round(value * 100))).toEqual([60, 40]);
  });

  it('rises on ArrowRight under ltr, and inside a dir="ltr" surface of an rtl page', () => {
    document.documentElement.setAttribute('dir', 'rtl');
    const seen: number[] = [];
    const container = mount(reading.ltr, () => new Slider(0.5, (value: number) => seen.push(value)));

    press(container, 'ArrowRight');
    press(container, 'ArrowLeft');

    expect(seen.map((value) => Math.round(value * 100))).toEqual([60, 40]);
  });

  it('leaves ArrowUp as up and ArrowDown as down in both directions', () => {
    for (const direction of [reading.rtl, reading.ltr]) {
      document.body.innerHTML = '';
      const seen: number[] = [];
      const container = mount(direction, () => new Slider(0.5, (value: number) => seen.push(value)));
      press(container, 'ArrowUp');
      press(container, 'ArrowDown');
      expect(seen.map((value) => Math.round(value * 100)), direction).toEqual([60, 40]);
    }
  });
});

describe('Calendar (#1569)', () => {
  const month = () => new Calendar(dateOnly(2026, 7, 17));
  const grid = (container: HTMLElement) => container.querySelector('[role="grid"]') as HTMLElement;
  /** The day the grid's active cell names. */
  const day = (container: HTMLElement): number => {
    const cell = container.querySelector(`#${grid(container).getAttribute('aria-activedescendant')}`);
    return Number(/(\d+) July/.exec(cell?.getAttribute('aria-label') ?? '')?.[1]);
  };

  it('walks to the next day on ArrowLeft and back on ArrowRight under rtl', async () => {
    const container = mount(reading.rtl, month);

    key(grid(container), 'ArrowLeft');
    await nextFrame();
    expect(day(container)).toBe(18);
    key(grid(container), 'ArrowLeft');
    await nextFrame();
    expect(day(container)).toBe(19);
    key(grid(container), 'ArrowRight');
    await nextFrame();
    expect(day(container)).toBe(18);
  });

  it('walks to the next day on ArrowRight under ltr, and inside a dir="ltr" surface of an rtl page', async () => {
    document.documentElement.setAttribute('dir', 'rtl');
    const container = mount(reading.ltr, month);

    key(grid(container), 'ArrowRight');
    await nextFrame();
    expect(day(container)).toBe(18);
    key(grid(container), 'ArrowLeft');
    await nextFrame();
    expect(day(container)).toBe(17);
  });

  it('leaves ArrowDown as the next week and ArrowUp as the previous in both directions', async () => {
    for (const direction of [reading.rtl, reading.ltr]) {
      document.body.innerHTML = '';
      const container = mount(direction, month);
      key(grid(container), 'ArrowDown');
      await nextFrame();
      expect(day(container), direction).toBe(24);
      key(grid(container), 'ArrowUp');
      await nextFrame();
      expect(day(container), direction).toBe(17);
    }
  });
});

describe('TreeView (#1569)', () => {
  const leaf = (name: string) => Object.assign(new TreeNode(name, name), { isLeaf: true });
  const files = () =>
    new TreeView(
      [Object.assign(new TreeNode('src', 'src'), { children: [leaf('App.cs')] }), leaf('README.md')],
      null,
      () => {},
      null,
      { label: 'Files' },
    );
  const tree = (container: HTMLElement) => container.querySelector('[role="tree"]') as HTMLElement;
  const src = (container: HTMLElement) => container.querySelector('[aria-label="src"]') as HTMLElement;

  /** The tree with its first node the active one, closed. */
  async function atSrc(direction: TextDirectionValue): Promise<HTMLElement> {
    const container = mount(direction, files);
    key(tree(container), 'ArrowDown');
    await nextFrame();
    expect(src(container).getAttribute('aria-expanded')).toBe('false');
    return container;
  }

  it('opens a closed node on ArrowLeft and closes it on ArrowRight under rtl', async () => {
    const container = await atSrc(reading.rtl);

    key(tree(container), 'ArrowLeft');
    await nextFrame();
    expect(src(container).getAttribute('aria-expanded')).toBe('true');

    key(tree(container), 'ArrowRight');
    await nextFrame();
    expect(src(container).getAttribute('aria-expanded')).toBe('false');
  });

  it('opens a closed node on ArrowRight under ltr, and inside a dir="ltr" surface of an rtl page', async () => {
    document.documentElement.setAttribute('dir', 'rtl');
    const container = await atSrc(reading.ltr);

    key(tree(container), 'ArrowRight');
    await nextFrame();
    expect(src(container).getAttribute('aria-expanded')).toBe('true');

    key(tree(container), 'ArrowLeft');
    await nextFrame();
    expect(src(container).getAttribute('aria-expanded')).toBe('false');
  });

  it('leaves ArrowDown as the next row in both directions', async () => {
    for (const direction of [reading.rtl, reading.ltr]) {
      document.body.innerHTML = '';
      const container = await atSrc(direction);
      key(tree(container), 'ArrowDown');
      await nextFrame();
      const active = container.querySelector(`#${tree(container).getAttribute('aria-activedescendant')}`);
      expect(active?.getAttribute('aria-label'), direction).toBe('README.md');
    }
  });
});
