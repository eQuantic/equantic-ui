/**
 * Spec §06 EXIT MOTION — the reconciler's removal deferral: a departing `.eq-overlay` layer that
 * carries presence subtrees is REPARENTED to document.body (position:fixed keeps it visually
 * identical, and the diffed parent's index math stays exact), plays the reverse animation, and is
 * removed on animationend (jsdom never fires it — tests dispatch). The departed copy is pixels
 * only: listeners cleaned, pointer-events dead. A rapid re-open cross-fades: the fresh layer
 * enters in the container WHILE the old copy exits under body.
 */

import { afterEach, describe, expect, it } from 'vitest';
import { StatefulComponent } from '../core/component';
import { photonTheme } from './design-system.generated';
import { setPhotonTheme } from './photon-context';
import { Column, Text } from './vocabulary';
import { Dialog } from './components/Dialog';
import { DialogAction } from './components/DialogAction';

setPhotonTheme(photonTheme);

const nextFrame = () =>
  new Promise<void>((resolve) =>
    requestAnimationFrame(() => requestAnimationFrame(() => resolve())),
  );

class ToggleHost extends StatefulComponent {
  open = true;

  build(): Column {
    const column = new Column(8);
    column.add(new Text('page', 'caption'));
    if (this.open) {
      column.add(new Dialog('Bye?', 'Leaving.', [new DialogAction('Ok')]) as never);
    }
    return column as never;
  }

  toggle(open: boolean): void {
    this.setState(() => (this.open = open));
  }
}

const finishExit = () => {
  document.body
    .querySelectorAll('[data-eq-exiting] [data-eq-exit]')
    .forEach((el) => el.dispatchEvent(new Event('animationend')));
};

afterEach(() => {
  finishExit();
});

describe('presence exit (removal deferral)', () => {
  it('a departing overlay reparents to body, plays the exit, and removes on animationend', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);
    const host = new ToggleHost();
    host.mount(container);
    expect(container.querySelector('.eq-overlay')).not.toBeNull();

    host.toggle(false);
    await nextFrame();

    // Semantically gone from the page…
    expect(container.querySelector('.eq-overlay')).toBeNull();

    // …but the exiting copy lingers under body: marked, input-dead, playing the reverse class.
    const exiting = document.body.querySelector<HTMLElement>('.eq-overlay[data-eq-exiting]')!;
    expect(exiting).not.toBeNull();
    expect(exiting.parentElement).toBe(document.body);
    expect(exiting.style.pointerEvents).toBe('none');
    const layer = exiting.querySelector('[data-eq-exit]')!;
    expect(layer.classList.contains('eq-presence-exit-fade')).toBe(true);

    // animationend on every presence subtree removes the copy.
    finishExit();
    expect(document.body.querySelector('.eq-overlay[data-eq-exiting]')).toBeNull();

    container.remove();
  });

  it('a rapid re-open cross-fades: fresh layer enters while the old copy exits', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);
    const host = new ToggleHost();
    host.mount(container);

    host.toggle(false);
    await nextFrame();
    host.toggle(true);
    await nextFrame();

    // The NEW layer lives in the container (interactive), the OLD one under body (exiting).
    expect(container.querySelector('.eq-overlay')).not.toBeNull();
    expect(document.body.querySelector('.eq-overlay[data-eq-exiting]')).not.toBeNull();

    finishExit();
    expect(document.body.querySelector('.eq-overlay[data-eq-exiting]')).toBeNull();
    expect(container.querySelector('.eq-overlay')).not.toBeNull();

    container.remove();
  });
});
