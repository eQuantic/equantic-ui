/**
 * A horizontal drag is measured toward the END of the direction where its surface reads (#1569), driven as the
 * browser drives it: through the lowering of the catalog's own `Switch`, `SwipeableRow` and `Slider` under a
 * `Directionality`, with pointerdown and pointermove at coordinates. The direction is the nearest `[dir]` where the
 * surface sits, read when the finger goes down (`reading-direction.ts` says why it is not the computed style: a DOM
 * with no layout engine computes none from `dir`).
 *
 * Two things move with the direction and a spec must tell them apart: the TRAVEL the finger makes (what Min, Max, the
 * rest and the reported offset are measured in) and the PAINT (the physical `translate` the surface is moved by).
 * Each case below fails if only one of them follows.
 */
import { afterEach, describe, expect, it } from 'vitest';
import { StatefulComponent } from '../core/component';
import { photonTheme } from '../shared/design-system.generated';
import { setPhotonTheme } from '../shared/photon-context';
import { Slider } from '../shared/components/Slider';
import { SwipeableRow } from '../shared/components/SwipeableRow';
import { Switch } from '../shared/components/Switch';
import { Directionality, Text } from '../shared/vocabulary';
import type { TextDirectionValue } from '../shared/enums.generated';
import { resetDraggableController } from './draggable';

setPhotonTheme(photonTheme);

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

const surfaceOf = (container: HTMLElement): HTMLElement =>
  container.querySelector('[data-eq-drag]') as HTMLElement;
const translateOf = (surface: HTMLElement): string => surface.style.getPropertyValue('translate');

const at = (target: EventTarget, type: string, x: number): void => {
  target.dispatchEvent(
    new PointerEvent(type, { clientX: x, clientY: 100, bubbles: true, cancelable: true }),
  );
};
/** A finger that goes down at `from` and moves through `path`, leaving the surface where the last move put it. */
function hold(surface: HTMLElement, from: number, ...path: number[]): void {
  at(surface, 'pointerdown', from);
  for (const x of path) at(document, 'pointermove', x);
}
const lift = (x: number): void => at(document, 'pointerup', x);

describe('a horizontal drag is measured toward the end (#1569)', () => {
  afterEach(() => {
    resetDraggableController();
    document.documentElement.removeAttribute('dir');
    document.body.innerHTML = '';
  });

  describe('a Switch', () => {
    // Off, the thumb is at the START of its track and the 28px of travel lead toward the END: leftwards under rtl.
    const off = (direction: TextDirectionValue, changed: boolean[]): HTMLElement =>
      mount(direction, () => new Switch(false, () => changed.push(true)));

    it('turns on when dragged toward its end under rtl, and its thumb stays in the track', () => {
      const changed: boolean[] = [];
      const thumb = surfaceOf(off('rightToLeft', changed));
      const travel = Number(thumb.getAttribute('data-eq-drag-max'));
      expect(travel).toBeGreaterThan(0);
      expect(thumb.getAttribute('data-eq-drag-min')).toBe('0');

      // Leftwards by 20: the paint is the travel toward the end, 20px LEFT of where it sits at the start.
      hold(thumb, 200, 180);
      expect(translateOf(thumb)).toBe('-20px');
      // Far past the end of the track: clamped at the travel, never beyond.
      at(document, 'pointermove', 0);
      expect(translateOf(thumb)).toBe(`${-travel}px`);
      // And back toward the START, past where it began: clamped there, never out of the track on the other side.
      at(document, 'pointermove', 400);
      expect(translateOf(thumb)).toBe('0px');

      at(document, 'pointermove', 100);
      lift(100);
      expect(changed).toEqual([true]);
    });

    it('does not turn on when dragged toward its start under rtl', () => {
      const changed: boolean[] = [];
      const thumb = surfaceOf(off('rightToLeft', changed));

      hold(thumb, 200, 260);
      expect(translateOf(thumb)).toBe('0px');
      lift(260);
      expect(changed).toEqual([]);
    });

    it('turns on when dragged rightwards under ltr, as in English', () => {
      const changed: boolean[] = [];
      const thumb = surfaceOf(off('leftToRight', changed));

      hold(thumb, 200, 220);
      expect(translateOf(thumb)).toBe('20px');
      at(document, 'pointermove', 300);
      lift(300);
      expect(changed).toEqual([true]);
    });

    it('keeps the English drag inside a dir="ltr" surface of an rtl page', () => {
      document.documentElement.setAttribute('dir', 'rtl');
      const changed: boolean[] = [];
      const thumb = surfaceOf(off('leftToRight', changed));

      hold(thumb, 200, 220);
      expect(translateOf(thumb)).toBe('20px');
      at(document, 'pointermove', 300);
      lift(300);
      expect(changed).toEqual([true]);
    });

    it('reads the direction where the thumb sits when the finger goes down, not where it was lowered', () => {
      const changed: boolean[] = [];
      const container = off('leftToRight', changed);
      const thumb = surfaceOf(container);
      // A Directionality that changed under a kept position rewrote one attribute and nothing else.
      (thumb.closest('[dir]') as HTMLElement).setAttribute('dir', 'rtl');

      hold(thumb, 200, 180);
      expect(translateOf(thumb)).toBe('-20px');
    });
  });

  describe('a SwipeableRow', () => {
    // The action stands at the END and the surface slides toward the START, by up to the action's 96px, to uncover it.
    const row = (direction: TextDirectionValue, opened: boolean[]): HTMLElement =>
      mount(
        direction,
        () =>
          new SwipeableRow(new Text('row') as never, 'Delete', 'search', null, {
            onOpenChanged: (open: boolean) => opened.push(open),
          }),
      );

    it('opens by a swipe toward its start under rtl, which is rightwards', () => {
      const opened: boolean[] = [];
      const surface = surfaceOf(row('rightToLeft', opened));
      expect(surface.getAttribute('data-eq-drag-min')).toBe('-96');

      hold(surface, 100, 160);
      // Painted rightwards, to uncover the action on the left, and never past the action's width.
      expect(translateOf(surface)).toBe('60px');
      at(document, 'pointermove', 400);
      expect(translateOf(surface)).toBe('96px');
      at(document, 'pointermove', 160);
      lift(160);

      expect(opened).toEqual([true]);
    });

    it('does not move for a swipe toward its end under rtl, and stays shut', () => {
      const opened: boolean[] = [];
      const surface = surfaceOf(row('rightToLeft', opened));

      hold(surface, 100, 40);
      expect(translateOf(surface)).toBe('0px');
      lift(40);

      expect(opened).toEqual([false]);
    });

    it('opens by a swipe leftwards under ltr, as in English', () => {
      const opened: boolean[] = [];
      const surface = surfaceOf(row('leftToRight', opened));

      hold(surface, 100, 40);
      expect(translateOf(surface)).toBe('-60px');
      lift(40);

      expect(opened).toEqual([true]);
    });

    it('opens by a swipe leftwards inside a dir="ltr" surface of an rtl page', () => {
      document.documentElement.setAttribute('dir', 'rtl');
      const opened: boolean[] = [];
      const surface = surfaceOf(row('leftToRight', opened));

      hold(surface, 100, 40);
      expect(translateOf(surface)).toBe('-60px');
      lift(40);

      expect(opened).toEqual([true]);
    });
  });

  describe('a Slider', () => {
    // A normalized drag: a fraction of the surface's own extent, which only the browser can measure.
    const slider = (direction: TextDirectionValue, values: number[]): HTMLElement => {
      const container = mount(
        direction,
        () => new Slider(0.5, (value: number) => values.push(value)),
      );
      Object.defineProperty(surfaceOf(container), 'offsetWidth', { value: 200 });
      return surfaceOf(container);
    };

    it('rises when dragged toward its end under rtl: a quarter of the width leftwards is a quarter more', () => {
      const values: number[] = [];
      const surface = slider('rightToLeft', values);

      hold(surface, 100, 50);

      expect(values[values.length - 1]).toBe(0.75);
    });

    it('falls when dragged toward its start under rtl', () => {
      const values: number[] = [];
      const surface = slider('rightToLeft', values);

      hold(surface, 100, 150);

      expect(values[values.length - 1]).toBe(0.25);
    });

    it('rises when dragged rightwards under ltr, and inside a dir="ltr" surface of an rtl page', () => {
      const english: number[] = [];
      hold(slider('leftToRight', english), 100, 150);
      expect(english[english.length - 1]).toBe(0.75);

      document.body.innerHTML = '';
      document.documentElement.setAttribute('dir', 'rtl');
      const fenced: number[] = [];
      hold(slider('leftToRight', fenced), 100, 150);
      expect(fenced[fenced.length - 1]).toBe(0.75);
    });
  });
});
