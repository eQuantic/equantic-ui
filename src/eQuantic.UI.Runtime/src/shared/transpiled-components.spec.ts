/**
 * The write-once proof on web, EXECUTED: the fixtures in `__transpiled__/` are the REAL eqc output
 * for `eQuantic.UI.Components.Shared` (pinned byte-for-byte by SharedComponentTranspilationTests on
 * the C# side). Here they run against the runtime vocabulary + the generated theme, and the DOM they
 * produce is asserted against the SAME values the C# WebRealizer tests pin — one C# source, compiled
 * twice (assembly for SSR/native, JS for the client), one rendering.
 */

import { describe, expect, it } from 'vitest';
import type { HtmlNode } from '../core/types';
import { Button } from './__transpiled__/Button.generated';
import { Card } from './__transpiled__/Card.generated';
import { Column, Text } from './vocabulary';

describe('transpiled shared Button (real eqc output)', () => {
  it('renders the same composition the C# WebRealizer pins (button > box > row > label)', () => {
    const button = new Button('Continuar');
    const node: HtmlNode = button.render();

    expect(node.tag).toBe('button');
    expect(node.attributes['aria-label']).toBe('Continuar');

    const box = node.children[0];
    expect(box.tag).toBe('div');
    // The Medium row of the spec A12 size table + Primary tokens — the WebRealizerTests values.
    expect(box.attributes.style).toContain('height: 40px');
    expect(box.attributes.style).toContain('min-width: 64px');
    expect(box.attributes.style).toContain('padding: 0 16px 0 16px');
    expect(box.attributes.style).toContain('background-color: light-dark(#0050a0, #5ca2e8)');
    expect(box.attributes.style).toContain('border-radius: 10px');

    const label = box.children[0].children[0];
    expect(label.tag).toBe('span');
    expect(label.attributes.style).toContain('font-size: 15px');
    expect(label.children[0].textContent).toBe('Continuar');
  });

  it('honors the C# parameter defaults (primary/medium) and explicit variants', () => {
    const outline = new Button('Editar', 'outline');
    const node = outline.render();
    const box = node.children[0];

    expect(box.attributes.style).toContain('border: 1px solid light-dark(#c9ced6, #3d4754)');
    expect(box.attributes.style).not.toContain('background-color');
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
      { disabled: true },
    );
    const node = disabled.render();

    expect(node.attributes.disabled).toBeDefined();
    expect(node.events.click).toBeUndefined();
    // Primary base with alpha 97 (255 × 0.38) → 8-digit hex on both modes.
    expect(node.children[0].attributes.style).toContain(
      'background-color: light-dark(#0050a061, #5ca2e861)',
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
    expect(node.attributes.style).toContain('border-radius: 14px');
    expect(node.attributes.style).toContain('padding: 16px 16px 16px 16px');
    expect(node.attributes.style).toContain('background-color: light-dark(#eff1f4, #1c232b)');
  });

  it('composes transpiled components inside abstract trees (Card wrapping a Column with a Button)', () => {
    const content = new Column(12);
    content.add(new Text('Portfolio overview', 'title'));
    content.add(new Button('Transferir') as never);

    const node = new Card(content, 'outlined').render();

    const column = node.children[0];
    expect(column.attributes.style).toContain('flex-direction: column');
    expect(column.children[1].tag).toBe('button');
  });
});
