/**
 * The owned click guard (#1889): what a drag's, a hold's and a pan's release arm so the click they end in reaches
 * nothing, and which goes with that click, with its owner, or after its time.
 */
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from 'vitest';
import { clickGuards } from './click-guard';

function click(target: EventTarget = document.body): MouseEvent {
  const event = new MouseEvent('click', { bubbles: true, cancelable: true });
  target.dispatchEvent(event);
  return event;
}

describe('an owned click guard', () => {
  let heard: Mock<(event: Event) => void>;

  beforeEach(() => {
    vi.useFakeTimers();
    heard = vi.fn<(event: Event) => void>();
    document.body.addEventListener('click', heard);
  });

  afterEach(() => {
    document.body.removeEventListener('click', heard);
    vi.useRealTimers();
  });

  it('stops the next click before anything under the pointer hears it, and goes with it, timer and all', () => {
    const owner = clickGuards();
    owner.arm(document, 600);
    expect(vi.getTimerCount()).toBe(1);

    const stopped = click();
    expect(stopped.defaultPrevented, 'the click the release ends in does nothing').toBe(true);
    expect(heard).not.toHaveBeenCalled();
    expect(vi.getTimerCount(), 'its timer went with the click it stopped').toBe(0);

    click();
    expect(heard, 'and the next click is the page’s').toHaveBeenCalledTimes(1);
  });

  it('lets go after its time when no click came, so a release that sends none does not eat the next real one', () => {
    const owner = clickGuards();
    owner.arm(document, 50);
    vi.advanceTimersByTime(49);
    vi.advanceTimersByTime(1);

    const next = click();
    expect(next.defaultPrevented).toBe(false);
    expect(heard).toHaveBeenCalledTimes(1);
  });

  it('goes at once when its owner lets go, with every guard that owner holds and none of another owner’s', () => {
    const owner = clickGuards();
    const other = clickGuards();
    owner.arm(document, 600);
    owner.arm(window, 400);
    other.arm(document, 50);
    expect(vi.getTimerCount()).toBe(3);

    owner.release();
    expect(vi.getTimerCount(), 'the owner’s two timers went, the other owner’s stays').toBe(1);
    expect(click().defaultPrevented, 'the other owner’s guard still stops its click').toBe(true);
    expect(vi.getTimerCount()).toBe(0);

    click();
    expect(heard, 'nothing of the owner’s is left to stop the next one').toHaveBeenCalledTimes(1);
  });

  it('lets go of nothing twice: a guard that went with its click is not let go again by its owner', () => {
    const owner = clickGuards();
    owner.arm(document, 600);
    click();
    const removed = vi.spyOn(document, 'removeEventListener');
    owner.release();

    expect(removed, 'the guard already went with its click').not.toHaveBeenCalled();
    removed.mockRestore();
  });
});
