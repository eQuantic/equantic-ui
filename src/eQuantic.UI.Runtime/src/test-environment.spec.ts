import { describe, expect, it } from 'vitest';

// The environment the setup file restores, held as the standard states it: if happy-dom drops the
// method or the setup file stops loading, this file fails on its own, not a spreadsheet spec that
// happens to glide a mark.
describe('the test environment follows Web Animations', () => {
  const glide = [{ translate: '10px 0px' }, { translate: '0px 0px' }];

  it('raises nothing when a canceled animation is awaited by nobody', async () => {
    const mark = document.createElement('div');
    document.body.appendChild(mark);
    mark.animate(glide, { duration: 1000 }).cancel();
    // An unhandled rejection is reported after the microtasks of the test drain, and vitest fails
    // the run with it: the wait is what makes this case an assertion.
    await new Promise((resolve) => setTimeout(resolve, 0));
    mark.remove();
  });

  it('still rejects with an AbortError for a caller that awaits it', async () => {
    const mark = document.createElement('div');
    document.body.appendChild(mark);
    const motion = mark.animate(glide, { duration: 1000 });
    const finished = motion.finished;
    motion.cancel();
    await expect(finished).rejects.toMatchObject({ name: 'AbortError' });
    mark.remove();
  });
});
