/**
 * The STATEFUL write-once proof, executed: `__transpiled__/SharedCounter.ts` is the real eqc output
 * for the same authoring shape the native CounterAppTests component uses (fields + SetState + Build,
 * composing the shared Button with a named argument). Here the full web loop runs — mount to real
 * (happy-dom) DOM, click the lowered button, `setState` schedules a rAF re-render — and the DOM
 * reflects the new state, mirroring the native tap → SetState → rebuild test on Photon.
 */

import { describe, expect, it } from 'vitest';
import { SharedCounter } from './__transpiled__/SharedCounter';

const nextFrame = () =>
  new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));

describe('transpiled SharedCounter (real eqc output, direct-SetState shape)', () => {
  it('mounts, increments through the lowered click, and re-renders the count', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);

    const counter = new SharedCounter();
    counter.mount(container);

    expect(container.textContent).toContain('Count: 0');

    const button = container.querySelector('button')!;
    expect(button).not.toBeNull();
    expect(button.getAttribute('aria-label')).toBe('Increment');

    button.click();
    await nextFrame();
    expect(container.textContent).toContain('Count: 1');

    button.click();
    button.click();
    await nextFrame();
    expect(container.textContent).toContain('Count: 3');

    container.remove();
  });

  it('renders the Medium Primary button from the named-argument reordering (defaults preserved)', () => {
    const node = new SharedCounter().render();

    // Column → [Text, Button]; the button carries the Medium/Primary tokens because the C#
    // defaults survived the named-argument transpilation.
    const buttonNode = node.children[1];
    expect(buttonNode.tag).toBe('button');
    const box = buttonNode.children[0];
    expect(box.attributes.style).toContain('height: 40px');
    expect(box.attributes.style).toContain('background-color: light-dark(#0050a0, #5ca2e8)');
  });
});
