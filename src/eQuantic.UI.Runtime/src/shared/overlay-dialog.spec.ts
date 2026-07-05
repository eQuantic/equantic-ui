/**
 * Overlay + Dialog end to end (Phase C slice 1, REAL eqc output): declarative presence — the app
 * SHOWS the dialog by building it and resolves it through action callbacks; the scrim stays a dead
 * click unless dismissible arms it with onDismiss. Cross-pins the C# OverlayDialogRealizerTests.
 */

import { describe, expect, it } from 'vitest';
import { SharedStatefulComponent } from '../core/component';
import { photonTheme } from './design-system.generated';
import { lowerVisualNode, tokenValue } from './lowering';
import { setPhotonTheme } from './photon-context';
import { Box, BoxStyle, Column, Overlay, Text } from './vocabulary';
import { Dialog } from './components/Dialog';
import { DialogAction } from './components/DialogAction';

setPhotonTheme(photonTheme);

const nextFrame = () =>
  new Promise<void>((resolve) =>
    requestAnimationFrame(() => requestAnimationFrame(() => resolve())),
  );

const lower = (node: unknown) =>
  lowerVisualNode(node as never, {
    textPrimary: photonTheme.textPrimary,
    componentContext: { theme: photonTheme, typeScale: 1 },
  });

describe('overlay lowering (C# cross-pin)', () => {
  it('lowers to the generated fixed layer with the child inside', () => {
    const node = lower(new Overlay(new Box(new BoxStyle({ width: 10, height: 10 }))));

    expect(node.tag).toBe('div');
    expect(node.attributes['class']).toBe('eq-overlay');
    expect(node.attributes['style']).toBeUndefined();
    expect(node.children).toHaveLength(1);
  });

  it('the transpiled Dialog composes scrim + centered E5 card', () => {
    const node = lower(
      new Dialog('Delete card?', 'It will be removed.', [
        new DialogAction('Keep card', null, 'ghost'),
        new DialogAction('Delete', null, 'destructive'),
      ]),
    );

    expect(node.attributes['class']).toBe('eq-overlay');
    const html = JSON.stringify(node);
    expect(html).toContain(tokenValue(photonTheme.scrim));
    expect(html).toContain('border-radius: 20px');
    expect(html).toContain('max-width: 480px');
  });
});

/** Confirm flow host: the dialog exists only while state says so. */
class ConfirmHost extends SharedStatefulComponent {
  confirming = false;
  outcome = 'none';

  build(): Column {
    const column = new Column(8);
    column.add(new Text(`outcome:${this.outcome}`, 'caption'));
    if (this.confirming) {
      column.add(
        new Dialog('Delete card?', 'It will be removed.', [
          new DialogAction(
            'Keep card',
            () =>
              this.setState(() => {
                this.outcome = 'kept';
                this.confirming = false;
              }),
            'ghost',
          ),
          new DialogAction(
            'Delete',
            () =>
              this.setState(() => {
                this.outcome = 'deleted';
                this.confirming = false;
              }),
            'destructive',
          ),
        ]) as never,
      );
    }
    return column as never;
  }

  open(): void {
    this.setState(() => (this.confirming = true));
  }
}

class DismissibleHost extends SharedStatefulComponent {
  open = true;
  dismissed = 0;

  build(): Column {
    const column = new Column(8);
    column.add(new Text(`d:${this.dismissed}`, 'caption'));
    if (this.open) {
      column.add(
        new Dialog('Sign out?', 'Anytime.', [new DialogAction('Stay')], true, () =>
          this.setState(() => {
            this.dismissed++;
            this.open = false;
          }),
        ) as never,
      );
    }
    return column as never;
  }
}

describe('Dialog end to end (transpiled component, declarative presence)', () => {
  it('appears with state, resolves through an action, and leaves the tree', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);
    const host = new ConfirmHost();
    host.mount(container);

    expect(container.querySelector('.eq-overlay')).toBeNull();

    host.open();
    await nextFrame();
    expect(container.querySelector('.eq-overlay')).not.toBeNull();
    expect(container.textContent).toContain('Delete card?');

    const scrim = container.querySelector<HTMLButtonElement>('.eq-overlay button')!;
    expect(scrim.disabled).toBe(true);

    const del = [...container.querySelectorAll('button')].find((b) =>
      b.textContent!.includes('Delete'),
    )!;
    del.click();
    await nextFrame();

    expect(container.textContent).toContain('outcome:deleted');
    expect(container.querySelector('.eq-overlay')).toBeNull();

    container.remove();
  });

  it('a dismissible dialog closes on scrim tap through onDismiss', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);
    new DismissibleHost().mount(container);

    const scrim = container.querySelector<HTMLButtonElement>('.eq-overlay button')!;
    expect(scrim.disabled).toBe(false);

    scrim.click();
    await nextFrame();
    expect(container.textContent).toContain('d:1');
    expect(container.querySelector('.eq-overlay')).toBeNull();

    container.remove();
  });
});
