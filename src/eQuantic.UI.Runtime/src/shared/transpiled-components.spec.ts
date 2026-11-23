/**
 * The write-once proof on web, EXECUTED: the modules in `components/` are the REAL eqc output
 * for `eQuantic.UI.Components` (pinned byte-for-byte by SharedComponentTranspilationTests on
 * the C# side). Here they run against the runtime vocabulary + the generated theme, and the DOM they
 * produce is asserted against the SAME values the C# WebRealizer tests pin — one C# source, compiled
 * twice (assembly for SSR/native, JS for the client), one rendering.
 */

import { effectiveStyle } from './style-atomizer';
import { describe, expect, it } from 'vitest';
import type { HtmlNode } from '../core/types';
import { Button } from './components/Button';
import { Card } from './components/Card';
import { Column, Text } from './vocabulary';
import { photonTheme } from './design-system.generated';
import { setPhotonTheme } from './photon-context';
import { ProgressBar } from './components/ProgressBar';
import { RadioGroup } from './components/RadioGroup';
import { AppThemeExtensions, ButtonTheme, ProgressTheme } from './theme-slots';

describe('transpiled shared Button (real eqc output)', () => {
  it('renders the same composition the C# WebRealizer pins (button > box > row > label)', () => {
    const button = new Button('Continuar');
    const node: HtmlNode = button.render();

    expect(node.tag).toBe('button');
    expect(node.attributes['aria-label']).toBe('Continuar');

    const box = node.children[0];
    expect(box.tag).toBe('div');
    // The Medium row of the spec A12 size table + Primary tokens — the WebRealizerTests values.
    expect(effectiveStyle(box)).toContain('height: 40px');
    expect(effectiveStyle(box)).toContain('min-width: 64px');
    expect(effectiveStyle(box)).toContain('padding: 0 16px 0 16px');
    expect(effectiveStyle(box)).toContain('background-color: light-dark(#0050a0, #76baff)');
    expect(effectiveStyle(box)).toContain('border-radius: 10px');

    const label = box.children[0].children[0];
    expect(label.tag).toBe('span');
    expect(effectiveStyle(label)).toContain('font-size: 15px');
    expect(label.children[0].textContent).toBe('Continuar');
  });

  // The instance's own style is the first step of the slot order (#1606), as in the C# Merge the twin
  // was transpiled from: what it names wins over the theme's Button slot, what it leaves null is the slot's.
  it("lays its own style over the theme's Button slot, member by member", () => {
    setPhotonTheme(AppThemeExtensions.with(photonTheme, null, new ButtonTheme({ minWidth: 173, outlineWidth: 3 })));
    try {
      const own = new Button('Editar', 'outline', 'medium', null, new ButtonTheme({ minWidth: 191 })).render();
      expect(effectiveStyle(own.children[0])).toContain('min-width: 191px');
      expect(effectiveStyle(own.children[0])).toContain('border: 3px solid');

      const plain = new Button('Editar', 'outline').render();
      expect(effectiveStyle(plain.children[0])).toContain('min-width: 173px');
    } finally {
      setPhotonTheme(photonTheme);
    }
  });

  it('honors the C# parameter defaults (primary/medium) and explicit variants', () => {
    const outline = new Button('Editar', 'outline');
    const node = outline.render();
    const box = node.children[0];

    expect(effectiveStyle(box)).toContain('border: 1px solid light-dark(#c9ced6, #4c5664)');
    expect(effectiveStyle(box)).not.toContain('background-color');
  });

  it('disabled applies the 38% token-level opacity group and swallows the press', () => {
    let fired = false;
    const disabled = new Button(
      'Salvar',
      'primary',
      'medium',
      () => {
        fired = true;
      },
      null,
      { disabled: true },
    );
    const node = disabled.render();

    expect(node.attributes.disabled).toBeDefined();
    expect(node.events.click).toBeUndefined();
    // Primary base with alpha 97 (255 × 0.38) → 8-digit hex on both modes.
    expect(effectiveStyle(node.children[0])).toContain(
      'background-color: light-dark(#0050a061, #76baff61)',
    );
    expect(fired).toBe(false);
  });

  it('fires onPressed through the lowered click event', () => {
    let count = 0;
    const button = new Button('Somar', 'primary', 'medium', () => {
      count++;
    });
    const node = button.render();
    (node.events.click as () => void)();
    expect(count).toBe(1);
  });
});

describe('transpiled shared Card (real eqc output)', () => {
  it('renders the filled surface with the spec radius and padding', () => {
    const card = new Card(new Text('body'), 'filled');
    const node = card.render();

    expect(node.tag).toBe('div');
    expect(effectiveStyle(node)).toContain('border-radius: 14px');
    expect(effectiveStyle(node)).toContain('padding: 16px 16px 16px 16px');
    expect(effectiveStyle(node)).toContain('background-color: light-dark(#eff1f4, #1c232b)');
  });

  it('composes transpiled components inside abstract trees (Card wrapping a Column with a Button)', () => {
    const content = new Column(12);
    content.add(new Text('Portfolio overview', 'title'));
    content.add(new Button('Transferir') as never);

    const node = new Card(content, 'outlined').render();

    const column = node.children[0];
    expect(effectiveStyle(column)).toContain('flex-direction: column');
    expect(column.children[1].tag).toBe('button');
  });
});

// The family's own style and the option builder, in the browser's twins (#1606).
describe("the family's own style and option builder (real eqc output)", () => {
  const collect = (node: HtmlNode, found: HtmlNode[] = []): HtmlNode[] => {
    found.push(node);
    for (const child of node.children ?? [])
      if (typeof child === 'object' && child !== null && 'tag' in child) collect(child as HtmlNode, found);
    return found;
  };

  it('a retained bar adopts the style its parent hands it', () => {
    const bar = new ProgressBar(0.4, 'primary', new ProgressTheme({ barHeight: 7 }));
    expect(effectiveStyle(bar.render().children[0] as HtmlNode)).toContain('height: 7px');

    bar.adoptConfig(new ProgressBar(0.4, 'primary', new ProgressTheme({ barHeight: 11 })));
    expect(effectiveStyle(bar.render().children[0] as HtmlNode)).toContain('height: 11px');
  });

  it("draws what the builder gives while each radio keeps its option's name", () => {
    const shown = ['Standard, in five days', 'Express, tomorrow'];
    const node = new RadioGroup(['Standard', 'Express'], 1, () => {}, null, (i: number) => new Text(shown[i])).render();
    const all = collect(node);

    const radios = all.filter((n) => n.attributes?.role === 'radio');
    expect(radios.map((r) => r.attributes['aria-label'])).toEqual(['Standard', 'Express']);
    const texts = all.map((n) => n.textContent).filter((t): t is string => typeof t === 'string');
    expect(texts).toEqual(expect.arrayContaining(shown));
    expect(texts).not.toContain('Standard');
  });
});
