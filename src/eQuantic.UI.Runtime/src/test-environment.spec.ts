import { describe, expect, it } from 'vitest';

// The environment the setup file restores, held as the standard states it: if happy-dom drops the
// method or the setup file stops loading, this file fails on its own, not a spreadsheet spec that
// happens to glide a mark.
// The frame the setup file restores (#1800): a test waits a frame for what a component scheduled, and a
// component's `Task.Yield` is a 0 ms timer, so a frame that outruns the timers is a wait that bounds
// nothing.
describe('the test environment draws a frame after the timers already due, as a browser does', () => {
  const frame = () => new Promise<number>((resolve) => requestAnimationFrame(resolve));

  it('runs a 0 ms timer set before a frame was asked for ahead of that frame', async () => {
    const order: string[] = [];
    setTimeout(() => order.push('timer'), 0);
    await frame().then(() => order.push('frame'));
    expect(order).toEqual(['timer', 'frame']);
  });

  it('runs every 0 ms timer set before two frames ahead of the second, however little the frames hold', async () => {
    const fired: number[] = [];
    for (let timer = 0; timer < 5; timer++) setTimeout(() => fired.push(timer), 0);
    await frame();
    await frame();
    expect(fired).toEqual([0, 1, 2, 3, 4]);
  });

  it('hands the frame its time, as performance.now() reads it', async () => {
    const before = performance.now();
    const time = await frame();
    expect(time).toBeGreaterThanOrEqual(before);
  });

  it('cancels a frame before its timer turn and after it, before it is drawn', async () => {
    const drawn: string[] = [];
    const early = requestAnimationFrame(() => drawn.push('early'));
    cancelAnimationFrame(early);
    const late = requestAnimationFrame(() => drawn.push('late'));
    // This timer is set after the frame's own, so it runs once the frame has been handed to the DOM
    // and before the DOM draws it.
    setTimeout(() => cancelAnimationFrame(late), 0);
    await frame();
    await frame();
    expect(drawn).toEqual([]);
  });
});

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
