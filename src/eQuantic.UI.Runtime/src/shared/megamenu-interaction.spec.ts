/**
 * The mega-menu interaction end to end — the exact structure the site's SiteHeader uses:
 * Pinned > Anchored(anchor: bar with Hoverable(Pressable), panel, ScrimStyle) driven by component
 * state. Press must open the shared surface (scrim + panel appear), outside-tap must dismiss, and
 * Hoverable's mouseenter must open it too. This is the integration the browser exercises after
 * hydration; failures here reproduce site defects deterministically.
 */

import { describe, expect, it } from 'vitest';
import { StatefulComponent, StatelessComponent } from '../core/component';
import {
  Anchored,
  Box,
  BoxStyle,
  Column,
  Hoverable,
  Pressable,
  Row,
  Pinned,
  Text,
} from './vocabulary';

const nextFrame = () =>
  new Promise<void>((resolve) =>
    requestAnimationFrame(() => requestAnimationFrame(() => resolve())),
  );

class MenuHeader extends StatefulComponent {
  _open = '';

  toggle(id: string) {
    this.setState(() => (this._open = this._open === id ? '' : id));
  }

  hoverChanged(id: string, entered: boolean) {
    if (entered) this.setState(() => (this._open = id));
  }

  build(): Pinned {
    const bar = new Row(8);
    bar.add(
      new Hoverable(
        new Pressable(new Text('Products', 'label'), () => this.toggle('products'), {
          label: 'Products',
        }),
        (entered: boolean) => this.hoverChanged('products', entered),
      ),
    );

    const panel = new Column(0);
    panel.add(new Text(this._open === 'products' ? 'PANEL-PRODUCTS' : 'PANEL-NONE', 'label'));

    return new Pinned(
      new Anchored(new Box(new BoxStyle({}), bar), panel, {
        open: this._open.length > 0,
        onDismiss: () => this.setState(() => (this._open = '')),
        scrimStyle: new BoxStyle({}) as unknown as import('./nodes').BoxStyleValue,
        gap: 0,
        matchAnchorWidth: true,
      }),
    );
  }
}

describe('mega-menu interaction (Pinned > Anchored + Hoverable(Pressable) + state)', () => {
  it('press opens the surface, outside-tap dismisses', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);

    const header = new MenuHeader();
    header.mount(container);

    expect(container.querySelector('.eq-anchor-panel')).toBeNull();

    const trigger = Array.from(container.querySelectorAll('button')).find(
      (b) => b.getAttribute('aria-label') === 'Products',
    )!;
    expect(trigger).toBeTruthy();
    trigger.click();
    await nextFrame();

    expect(container.querySelector('.eq-anchor-panel')).not.toBeNull();
    expect(container.textContent).toContain('PANEL-PRODUCTS');
    const scrim = container.querySelector('.eq-anchor-scrim') as HTMLButtonElement;
    expect(scrim).not.toBeNull();

    scrim.click();
    await nextFrame();
    expect(container.querySelector('.eq-anchor-panel')).toBeNull();

    container.remove();
  });

  it('press opens the surface AFTER HYDRATION (the SSR path the site takes)', async () => {
    // Produce the server markup: a fresh render captured as HTML (the C# SSR emits the same tree
    // by the cross-pin contract), then hydrate a NEW component instance over it.
    const ssrSource = document.createElement('div');
    document.body.appendChild(ssrSource);
    new MenuHeader().mount(ssrSource);
    const ssrHtml = ssrSource.innerHTML;
    ssrSource.remove();

    const container = document.createElement('div');
    container.innerHTML = ssrHtml;
    document.body.appendChild(container);

    const header = new MenuHeader();
    header.hydrate(container);
    await nextFrame();

    const trigger = Array.from(container.querySelectorAll('button')).find(
      (b) => b.getAttribute('aria-label') === 'Products',
    )!;
    expect(trigger).toBeTruthy();
    trigger.click();
    await nextFrame();

    expect(container.querySelector('.eq-anchor-panel')).not.toBeNull();
    expect(container.textContent).toContain('PANEL-PRODUCTS');

    container.remove();
  });

  it('press opens the surface when the header is a CHILD of a stateless page (the site shape)', async () => {
    // The site's HomePage is a STATELESS root composing the stateful SiteHeader — the child's
    // setState must still find a re-render path.
    class Page extends StatelessComponent {
      build(): Column {
        const page = new Column(0);
        page.add(new MenuHeader() as unknown as Text);
        return page;
      }
    }

    const container = document.createElement('div');
    document.body.appendChild(container);
    new Page().mount(container);
    await nextFrame();

    const trigger = Array.from(container.querySelectorAll('button')).find(
      (b) => b.getAttribute('aria-label') === 'Products',
    )!;
    expect(trigger).toBeTruthy();
    trigger.click();
    await nextFrame();

    expect(container.querySelector('.eq-anchor-panel')).not.toBeNull();
    container.remove();
  });

  it('hoverable mouseenter opens the surface', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);

    const header = new MenuHeader();
    header.mount(container);

    const trigger = Array.from(container.querySelectorAll('button')).find(
      (b) => b.getAttribute('aria-label') === 'Products',
    )!;
    const hoverHost = trigger.parentElement!;
    hoverHost.dispatchEvent(new MouseEvent('mouseenter', { bubbles: false }));
    await nextFrame();

    expect(container.querySelector('.eq-anchor-panel')).not.toBeNull();

    container.remove();
  });
});
