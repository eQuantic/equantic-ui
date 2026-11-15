/**
 * Gestures v2 — the web drag-to-dismiss controller: ONE document-level pointer delegate drives the
 * sheet contract on `[data-eq-drag-dismiss]` surfaces. Sequences dispatch MouseEvents with pointer
 * type names (jsdom runs no CSS, so the glide is asserted as the transition+transform styles).
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { resetDragDismissController } from '../dom/drag-dismiss';
import { StatefulComponent } from '../core/component';
import { photonTheme } from './design-system.generated';
import { setPhotonTheme } from './photon-context';
import { Column, Text } from './vocabulary';
import { BottomSheet } from './components/BottomSheet';

setPhotonTheme(photonTheme);

const nextFrame = () =>
  new Promise<void>((resolve) =>
    requestAnimationFrame(() => requestAnimationFrame(() => resolve())),
  );

class SheetHost extends StatefulComponent {
  open = true;
  dismissals = 0;

  build(): Column {
    const column = new Column(8);
    column.add(new Text('page', 'caption'));
    if (this.open) {
      column.add(
        new BottomSheet(new Text('content', 'bodyM'), () =>
          this.setState(() => {
            this.dismissals++;
            this.open = false;
          }),
        ) as never,
      );
    }
    return column as never;
  }
}

const pointer = (type: string, target: EventTarget, clientY: number) =>
  target.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, clientY }));

const finishExit = () => {
  document.body
    .querySelectorAll('[data-eq-exiting] [data-eq-exit]')
    .forEach((el) => el.dispatchEvent(new Event('animationend')));
};

describe('drag-to-dismiss controller (web)', () => {
  let container: HTMLElement;
  let host: SheetHost;
  let sheet: HTMLElement;

  beforeEach(() => {
    resetDragDismissController();
    container = document.createElement('div');
    document.body.appendChild(container);
    host = new SheetHost();
    host.mount(container);
    sheet = container.querySelector<HTMLElement>('[data-eq-drag-dismiss]')!;
    expect(sheet).not.toBeNull();
    expect(sheet.getAttribute('data-eq-drag-dismiss')).toBe('96');
  });

  afterEach(() => {
    finishExit();
    container.remove();
  });

  it('follows past the slop and glides back on a short release', () => {
    pointer('pointerdown', sheet, 300);
    pointer('pointermove', document, 340); // dy 40 — past the 12dp slop
    expect(sheet.style.transform).toBe('translateY(40px)');

    pointer('pointerup', document, 340); // short of 96 → glide back
    expect(sheet.style.transform).toBe('');
    expect(sheet.style.transition).toContain('transform');
    expect(host.dismissals).toBe(0);
  });

  it('a release past the threshold dismisses — and the exit motion takes over', async () => {
    pointer('pointerdown', sheet, 300);
    pointer('pointermove', document, 420); // dy 120 — past ThresholdDp
    pointer('pointerup', document, 420);
    await nextFrame();

    expect(host.dismissals).toBe(1);
    // The overlay left the container; the exit copy lingers under body until animationend.
    expect(container.querySelector('.eq-overlay')).toBeNull();
    expect(document.body.querySelector('.eq-overlay[data-eq-exiting]')).not.toBeNull();
  });

  it('a sub-slop wiggle never engages the drag', () => {
    pointer('pointerdown', sheet, 300);
    pointer('pointermove', document, 308); // dy 8 < slop 12
    expect(sheet.style.transform).toBe('');
    pointer('pointerup', document, 308);
    expect(host.dismissals).toBe(0);
  });

  /**
   * Under a parent drawn at twice the size, the threshold is the sheet's own 96: 120 on the screen is
   * 60 of it, short, and the sheet is moved 60 in its parent's space, which draws it under the finger.
   * Read on the screen, the same move dismissed it, and drew it 240 away. Photon counts it in the
   * sheet's own space (#734); measured in Chromium (#1492). jsdom reports a transform as it was
   * written, so the scale is written as the matrix a browser computes from it.
   */
  it('counts its threshold in its own pixels under a scaled parent', async () => {
    (sheet.parentElement as HTMLElement).style.transform = 'matrix(2, 0, 0, 2, 0, 0)';
    pointer('pointerdown', sheet, 300);
    pointer('pointermove', document, 420);
    expect(sheet.style.transform).toBe('translateY(60px)');

    pointer('pointerup', document, 420);
    await nextFrame();
    expect(host.dismissals).toBe(0);
  });

  it('arms by the distance on the screen under a scaled parent', () => {
    // A slop is the finger's: 16 on the screen arms past 12 at twice the size, where it is 8 of the
    // sheet's own pixels.
    (sheet.parentElement as HTMLElement).style.transform = 'matrix(2, 0, 0, 2, 0, 0)';
    pointer('pointerdown', sheet, 300);
    pointer('pointermove', document, 316);
    expect(sheet.style.transform).toBe('translateY(8px)');
    pointer('pointerup', document, 316);
  });
});
