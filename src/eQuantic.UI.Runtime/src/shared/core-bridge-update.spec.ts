import { describe, it, expect } from 'vitest';
import { StatefulComponent, ComponentState } from '../core/component';
import { VisualNodeComponent } from './visual-node-component';
import type { VisualNodeValue } from './nodes';

/**
 * The CORE page + write-once bridge update path (the transpiled Dashboard shape): a Core
 * StatefulComponent whose state builds a VisualNodeComponent — setState must re-render THROUGH the
 * bridge and patch the DOM. Regression: the atomic-style slice initially broke this (state mutated,
 * DOM stayed frozen).
 */
class CounterState extends ComponentState<CounterPage> {
  _count = 0;

  setState(fn: () => void): void {
    fn();
    (this as unknown as { _component: CounterPage })._component._scheduleRender();
  }

  build(): VisualNodeComponent {
    const text: VisualNodeValue = {
      nodeKind: 'text',
      content: `Count: ${this._count}`,
      role: 'heading',
    } as unknown as VisualNodeValue;
    const column: VisualNodeValue = {
      nodeKind: 'column',
      gap: 8,
      main: 'start',
      cross: 'stretch',
      children: [
        text,
        {
          nodeKind: 'pressable',
          label: 'inc',
          onPressed: () => this.setState(() => this._count++),
          child: text,
        } as unknown as VisualNodeValue,
      ],
    } as unknown as VisualNodeValue;
    return new VisualNodeComponent(column);
  }
}

class CounterPage extends StatefulComponent {
  createState(): CounterState {
    return new CounterState(); // the base render pass wires setComponent(this)
  }
}

describe('Core page + write-once bridge: setState patches the DOM', () => {
  it('a click re-renders through the bridge (rAF flush)', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);

    const page = new CounterPage();
    page.mount(container);
    expect(container.innerText || container.textContent).toContain('Count: 0');

    const button = container.querySelector('button')!;
    button.click();

    // _scheduleRender flushes on requestAnimationFrame.
    await new Promise((r) => requestAnimationFrame(() => r(null)));
    await new Promise((r) => setTimeout(r, 10));

    expect(container.textContent).toContain('Count: 1');
  });
});
