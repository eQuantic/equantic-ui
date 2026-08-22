/**
 * WCAG 1.4.13 for the hover/focus-revealed panel: Escape hides it WITHOUT moving pointer or
 * focus, and the suppression lasts exactly as long as the condition that revealed it — leaving
 * clears it, so the next visit reveals again. Esc is a "not now", never a "never again".
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { installHoverRevealSuppression, resetHoverRevealSuppression } from './hover-reveal';

describe('hover-reveal Escape suppression (WCAG 1.4.13)', () => {
  beforeEach(() => {
    document.body.innerHTML = `
      <div class="eq-anchorhost eq-hoverreveal">
        <button>Reset</button>
        <div class="eq-anchor-panel" role="tooltip" id="eq-tip-x">hint</div>
      </div>
      <div class="eq-anchorhost eq-hoverreveal" id="other">
        <button>Other</button>
        <div class="eq-anchor-panel">other hint</div>
      </div>`;
    installHoverRevealSuppression();
  });

  afterEach(() => {
    resetHoverRevealSuppression();
    document.body.innerHTML = '';
  });

  const host = () => document.querySelector('.eq-hoverreveal') as HTMLElement;
  // Dispatched from the FOCUSED element, the way a real Escape is born — it bubbles past body
  // (where a dialog would listen) up to the document (where the suppressor listens in capture).
  const escape = () =>
    (document.activeElement ?? document.body).dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }),
    );

  it('Escape suppresses the host holding focus — and only that one', () => {
    (host().querySelector('button') as HTMLButtonElement).focus();

    escape();

    expect(host().classList.contains('eq-reveal-suppressed')).toBe(true);
    expect(document.getElementById('other')!.classList.contains('eq-reveal-suppressed')).toBe(
      false,
    );
  });

  it('focus does not move — hiding the panel is the whole effect', () => {
    const button = host().querySelector('button') as HTMLButtonElement;
    button.focus();

    escape();

    expect(document.activeElement).toBe(button);
  });

  it('leaving the host clears the suppression, so the next visit reveals again', () => {
    const button = host().querySelector('button') as HTMLButtonElement;
    button.focus();
    escape();
    expect(host().classList.contains('eq-reveal-suppressed')).toBe(true);

    host().dispatchEvent(new FocusEvent('focusout'));

    expect(host().classList.contains('eq-reveal-suppressed')).toBe(false);
  });

  it('Escape with nothing revealed does nothing', () => {
    (document.activeElement as HTMLElement | null)?.blur?.();

    escape();

    expect(document.querySelectorAll('.eq-reveal-suppressed')).toHaveLength(0);
  });

  it('the event is not consumed — a dialog above still hears its own Escape', () => {
    (host().querySelector('button') as HTMLButtonElement).focus();
    let dialogHeard = false;
    document.body.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') dialogHeard = !e.defaultPrevented;
    });

    escape();

    expect(dialogHeard).toBe(true);
  });
});
