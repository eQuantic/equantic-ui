/**
 * Text input end to end (spec B9/B10, REAL eqc output): typing into the real <input> drives the
 * CONTROLLED loop — onChanged → app setState → rebuild → the positional reconciler retains the
 * TextInput instance while adoptConfig carries the fresh value in — and focus/blur drive the
 * component's INTERNAL state (2dp Primary border, padding compensation). The clear button routes ""
 * through onChanged. Cross-pins the C# TextInputRealizerTests byte-for-byte on the primitive.
 */

import type { HtmlNode } from '../core/types';
import { effectiveStyle, hashDeclaration } from './style-atomizer';
import { describe, expect, it } from 'vitest';
import { StatefulComponent } from '../core/component';
import { photonTheme } from './design-system.generated';
import { lowerVisualNode, tokenValue } from './lowering';
import { setPhotonTheme } from './photon-context';
import { Column, Text, TextEntry } from './vocabulary';
import { TextInput } from './components/TextInput';
import { SearchField } from './components/SearchField';

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

/** The entry now lowers inside the stable .eq-field shell \u2014 input first, sr-only twin second. */
const entryParts = (host: HtmlNode) => ({
  input: host.children[0]!,
  description: host.children[1]!,
});

describe('text entry primitive (C# cross-pin)', () => {
  it('the accessible name is the Label, never the placeholder', () => {
    const entry = new TextEntry('', null, { label: 'Recipient', placeholder: 'name@host' });
    const { input } = entryParts(lower(entry) as HtmlNode);
    expect(input.attributes['aria-label']).toBe('Recipient');

    const hintOnly = new TextEntry('', null, { placeholder: 'Search' });
    const { input: bare } = entryParts(lower(hintOnly) as HtmlNode);
    expect(bare.attributes['aria-label']).toBeUndefined();
    expect(bare.attributes['placeholder']).toBe('Search');
  });

  it('lowers to the real chrome-less input inside the stable field shell', () => {
    const node = lower(new TextEntry('ana@equantic', null, { placeholder: 'you@company.com' }));

    expect(node.tag).toBe('div');
    expect(node.attributes['class']).toMatch(/^eq-field(?: |$)/);
    expect(node.children).toHaveLength(2);

    const { input, description } = entryParts(node as HtmlNode);
    expect(input.tag).toBe('input');
    expect(input.attributes['class']).toMatch(/^eq-entry eq-type-bodyl(?: |$)/);
    expect(input.attributes['type']).toBe('text');
    expect(input.attributes['value']).toBe('ana@equantic');
    expect(input.attributes['placeholder']).toBe('you@company.com');
    expect(effectiveStyle(input)).toBe(
      `background: none; border: none; color: ${tokenValue(photonTheme.textPrimary)}; ` +
        `font-family: inherit; padding: 0; pointer-events: auto; width: 100%`,
    );

    // The twin is present (and empty) even without a description, so a later error swap
    // changes attributes instead of replacing the <input> and dropping focus.
    expect(description.tag).toBe('span');
    expect(description.attributes['class']).toMatch(/^eq-desc(?: |$)/);
    expect(description.attributes['aria-live']).toBe('polite');
    expect(description.attributes['id']).toBeUndefined();
    expect(input.attributes['aria-describedby']).toBeUndefined();
  });

  it('a description is associated by hashed id and announces politely (C# twin byte-for-byte)', () => {
    const node = lower(new TextEntry('', null, { description: 'We never share it.' }));
    const { input, description } = entryParts(node as HtmlNode);

    const id = `eq-desc-${hashDeclaration('We never share it.')}`;
    expect(description.attributes['id']).toBe(id);
    expect(input.attributes['aria-describedby']).toBe(id);
    expect(description.children[0]?.textContent).toBe('We never share it.');
    expect(input.attributes['aria-invalid']).toBeUndefined();
  });

  it('invalid is a state, never worded into the name', () => {
    const node = lower(new TextEntry('nope', null, { label: 'Email', invalid: true }));
    const { input } = entryParts(node as HtmlNode);
    expect(input.attributes['aria-invalid']).toBe('true');
    expect(input.attributes['aria-label']).toBe('Email');
  });

  it('several lines lower to a textarea carrying the value as content', () => {
    const node = lower(
      new TextEntry('Tell us about the project', null, {
        lines: 5,
        placeholder: 'Describe your request\u2026',
      }),
    );

    const { input: textarea } = entryParts(node as HtmlNode);
    expect(textarea.tag).toBe('textarea');
    expect(textarea.attributes['rows']).toBe('5');
    expect(textarea.attributes['value']).toBeUndefined();
    expect(textarea.children[0]?.textContent).toBe('Tell us about the project');
    expect(effectiveStyle(textarea)).toContain('resize: vertical');
  });

  it('obscure maps to type=password; disabled drops the handlers', () => {
    const { input: password } = entryParts(
      lower(new TextEntry('secret', null, { obscure: true })) as HtmlNode,
    );
    expect(password.attributes['type']).toBe('password');

    const { input: disabled } = entryParts(
      lower(new TextEntry('x', () => {}, { disabled: true })) as HtmlNode,
    );
    expect(disabled.attributes['disabled']).toBe('');
    expect(Object.keys(disabled.events)).toHaveLength(0);
  });
});

/** The controlled-form host: app state echoes what the field reports. */
class FormHost extends StatefulComponent {
  email = '';

  build(): Column {
    const column = new Column(8);
    column.add(
      new TextInput(
        this.email,
        (value: string) => this.setState(() => (this.email = value)),
        'Email',
      ) as never,
    );
    column.add(new Text(`echo:${this.email}`, 'caption'));
    return column as never;
  }
}

describe('TextInput end to end (transpiled component, controlled loop)', () => {
  it('typing drives onChanged → app rebuild → retained instance adopts the fresh value', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);
    new FormHost().mount(container);

    const input = container.querySelector('input')!;
    expect(input).not.toBeNull();

    input.value = 'ana@equantic';
    input.dispatchEvent(new Event('input', { bubbles: true }));
    await nextFrame();

    expect(container.textContent).toContain('echo:ana@equantic');
    expect(container.querySelector('input')!.value).toBe('ana@equantic');

    container.remove();
  });

  it('focus swaps to the 2dp Primary border and blur restores it (internal state)', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);
    new FormHost().mount(container);

    // Styles are atomic classes now — resolve a live element's effective declarations through
    // the registry seam before matching.
    const liveStyle = (el: Element) =>
      effectiveStyle({
        attributes: {
          class: el.getAttribute('class') ?? undefined,
          style: el.getAttribute('style') ?? undefined,
        },
      });
    const containerDiv = () =>
      [...container.querySelectorAll('div')].find((d) => liveStyle(d).includes('border:'))!;
    const primary = tokenValue(photonTheme.colors('primary').base);
    const strong = tokenValue(photonTheme.borderStrong);

    expect(liveStyle(containerDiv())).toContain(`border: 1px solid ${strong}`);
    expect(liveStyle(containerDiv())).toContain('padding: 0 14px 0 14px');

    container.querySelector('input')!.dispatchEvent(new Event('focus', { bubbles: true }));
    await nextFrame();
    expect(liveStyle(containerDiv())).toContain(`border: 2px solid ${primary}`);
    expect(liveStyle(containerDiv())).toContain('padding: 0 13px 0 13px');

    container.querySelector('input')!.dispatchEvent(new Event('blur', { bubbles: true }));
    await nextFrame();
    expect(liveStyle(containerDiv())).toContain(`border: 1px solid ${strong}`);

    container.remove();
  });
});

/** Search host: clear must route "" through onChanged (the controlled model). */
class SearchHost extends StatefulComponent {
  query = 'rio';
  submitted = 0;

  build(): Column {
    const column = new Column(8);
    column.add(
      new SearchField(
        this.query,
        (value: string) => this.setState(() => (this.query = value)),
        'Search…',
        () => this.setState(() => this.submitted++),
      ) as never,
    );
    column.add(new Text(`q:${this.query}|s:${this.submitted}`, 'caption'));
    return column as never;
  }
}

describe('SearchField end to end (transpiled component)', () => {
  it('clear routes an empty query through onChanged; Enter fires onSubmit', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);
    new SearchHost().mount(container);

    expect(container.textContent).toContain('q:rio|s:0');
    expect(container.querySelector('input')!.getAttribute('aria-label')).toBe(
      'Search…',
      // The pill has no visible label — the placeholder text is promoted to the real name.
    );
    const clear = container.querySelector('button')!;
    expect(clear.getAttribute('aria-label')).toBe('Clear search');

    clear.click();
    await nextFrame();
    expect(container.textContent).toContain('q:|s:0');
    expect(container.querySelector('input')!.value).toBe('');
    expect(container.querySelector('button')).toBeNull();

    container
      .querySelector('input')!
      .dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    await nextFrame();
    expect(container.textContent).toContain('|s:1');

    container.remove();
  });
});
