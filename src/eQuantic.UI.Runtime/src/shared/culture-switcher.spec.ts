/**
 * The language switch as the browser sees it after hydration.
 *
 * The C# and this TypeScript are twins by hand, and a change to one that misses the other does
 * not fail a build — it ships a page whose server markup and hydrated markup disagree, which is
 * exactly how `Shape = Menu` first reached the site as three segments. So the shape choice and
 * what each shape puts on screen are asserted HERE, where a mismatch is a red test.
 */

import { describe, expect, it } from 'vitest';
import { configureServices, resetServiceProvider } from '../core/service-provider';
import { CultureOption } from './__transpiled__/CultureOption';
import { CultureSwitcher } from './__transpiled__/CultureSwitcher';

const nextFrame = () =>
  new Promise<void>((resolve) =>
    requestAnimationFrame(() => requestAnimationFrame(() => resolve())),
  );

const LANGUAGES = [
  new CultureOption('en', 'English', 'EN', '🇬🇧'),
  new CultureOption('pt-BR', 'Português', 'PT', '🇧🇷'),
  new CultureOption('es', 'Español', 'ES', '🇪🇸'),
];

/** Enough of ICultureController for the component: which culture we are in, and a record of what
 * a switch asked for. */
class FakeCulture {
  applied: string[] = [];
  constructor(public uICulture: string) {}
  apply(culture: string) {
    this.applied.push(culture);
  }
}

function mount(switcher: CultureSwitcher, culture = 'en') {
  const controller = new FakeCulture(culture);
  resetServiceProvider();
  configureServices((services) => services.registerInstance('ICultureController', controller));
  const container = document.createElement('div');
  document.body.appendChild(container);
  switcher.mount(container);
  return { container, controller };
}

describe('CultureSwitcher', () => {
  it('is three segments by default, because three languages fit', () => {
    const { container } = mount(new CultureSwitcher(LANGUAGES));
    expect(container.textContent).toContain('English');
    expect(container.textContent).toContain('Português');
    expect(container.textContent).toContain('Español');
    container.remove();
  });

  it('Shape.menu shows the CURRENT language as a short code, not all three names', () => {
    const { container } = mount(new CultureSwitcher(LANGUAGES, { shape: 'menu' }), 'pt-BR');
    expect(container.textContent).toContain('PT');
    // The point of the menu shape: a crowded header pays for one control, not three names.
    expect(container.textContent).not.toContain('English');
    expect(container.textContent).not.toContain('Español');
    container.remove();
  });

  it('Shape.menu opens a menu of flag + endonym, and choosing one switches', async () => {
    const { container, controller } = mount(new CultureSwitcher(LANGUAGES, { shape: 'menu' }));

    const trigger = container.querySelector('[aria-haspopup="menu"]') as HTMLElement;
    expect(trigger).not.toBeNull();
    trigger.click();
    await nextFrame();

    const panel = container.querySelector('.eq-anchor-panel')!;
    expect(panel).not.toBeNull();
    expect(panel.textContent).toContain('🇬🇧');
    expect(panel.textContent).toContain('Português');
    expect(panel.textContent).toContain('🇪🇸');

    const items = Array.from(panel.querySelectorAll('[role="menuitem"]')) as HTMLElement[];
    expect(items).toHaveLength(3);
    items[1].click();
    await nextFrame();

    expect(controller.applied).toEqual(['pt-BR']);
    container.remove();
  });

  it('Shape.segments keeps segments past three languages, where auto would give up', () => {
    const many = [...LANGUAGES, new CultureOption('fr', 'Français', 'FR', '🇫🇷')];
    const { container } = mount(new CultureSwitcher(many, { shape: 'segments' }));
    expect(container.textContent).toContain('Français');
    expect(container.querySelector('[aria-haspopup="menu"]')).toBeNull();
    container.remove();
  });

  it('falls back to the endonym when an option carries no short code', () => {
    const bare = [new CultureOption('en', 'English'), new CultureOption('pt-BR', 'Português')];
    const { container } = mount(new CultureSwitcher(bare, { shape: 'menu' }));
    expect(container.textContent).toContain('English');
    container.remove();
  });
});
