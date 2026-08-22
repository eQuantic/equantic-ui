import { describe, it, expect, beforeEach } from 'vitest';
import { RenderManager } from './renderer';
import { resetReconciler } from './reconciler';
import { HtmlNode } from '../core/types';

/** A layout shell (`div.shell > [nav.nav, div.content > content]`) wrapping a content node. */
function shell(content: HtmlNode): HtmlNode {
  return {
    tag: 'div',
    attributes: { class: 'shell' },
    events: {},
    children: [
      { tag: 'nav', attributes: { class: 'nav' }, events: {}, children: [] },
      { tag: 'div', attributes: { class: 'content' }, events: {}, children: [content] },
    ],
  };
}

const text = (s: string): HtmlNode => ({
  tag: '#text',
  textContent: s,
  attributes: {},
  events: {},
  children: [],
});

describe('RenderManager.adopt — SPA-navigation persistent layout', () => {
  let container: HTMLElement;

  beforeEach(() => {
    resetReconciler();
    container = document.createElement('div');
    document.body.appendChild(container);
  });

  it('preserves the shared shell DOM and only patches the changed content', () => {
    // Page A mounts the shell with content "A".
    const rmA = new RenderManager();
    const treeA = shell(text('A'));
    rmA.mount(treeA, container);

    const shellEl = container.querySelector('.shell');
    const navEl = container.querySelector('.nav');
    const contentEl = container.querySelector('.content');
    expect(contentEl?.textContent).toBe('A');

    // Page B navigates in, reconciling against A's tree (what the boot does on SPA nav).
    const rmB = new RenderManager();
    rmB.adopt(shell(text('B')), container, treeA);

    // Shared shell nodes are the SAME DOM elements (not re-created)...
    expect(container.querySelector('.shell')).toBe(shellEl);
    expect(container.querySelector('.nav')).toBe(navEl);
    expect(container.querySelector('.content')).toBe(contentEl);
    // ...and only the content text was patched.
    expect(container.querySelector('.content')?.textContent).toBe('B');
  });

  it('keeps a listener attached to a preserved shell element across navigation', () => {
    let clicks = 0;
    const handler = () => {
      clicks++;
    };
    const shellWithButton = (content: HtmlNode): HtmlNode => ({
      tag: 'div',
      attributes: { class: 'shell' },
      events: {},
      children: [
        { tag: 'button', attributes: { class: 'menu' }, events: { click: handler }, children: [] },
        { tag: 'div', attributes: { class: 'content' }, events: {}, children: [content] },
      ],
    });

    const rmA = new RenderManager();
    const treeA = shellWithButton(text('A'));
    rmA.mount(treeA, container);
    (container.querySelector('.menu') as HTMLElement).click();
    expect(clicks).toBe(1);

    const rmB = new RenderManager();
    rmB.adopt(shellWithButton(text('B')), container, treeA);

    // The preserved button still fires its handler after navigation.
    (container.querySelector('.menu') as HTMLElement).click();
    expect(clicks).toBe(2);
  });

  it('falls back to a clean mount when there is no previous tree', () => {
    const rm = new RenderManager();
    rm.adopt(shell(text('X')), container, null);
    expect(container.querySelector('.content')?.textContent).toBe('X');
    expect(rm.getCurrentNode()).not.toBeNull();
  });
});
