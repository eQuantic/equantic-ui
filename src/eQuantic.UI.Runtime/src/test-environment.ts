/**
 * The DOM the runtime's tests run in, brought back to the standard where happy-dom departs from it.
 * Loaded by vitest before every spec file (`setupFiles`); nothing the runtime ships imports it.
 *
 * Web Animations' "cancel an animation" rejects the current finished promise with an AbortError
 * AND sets its [[PromiseIsHandled]] slot to true (https://www.w3.org/TR/web-animations-1/,
 * § 4.4.14, step 1.3), so a browser reports nothing when nobody awaits it. happy-dom 21, the first
 * version with `Element.animate`, rejects it unhandled: the table glide the runtime cancels when a
 * mark moves again mid-flight (`glideMarks`) failed the run with three "The animation was
 * canceled." rejections that no browser raises. Taking a handler on the promise before it is
 * rejected is what the slot means, and a caller that awaits it still sees the AbortError.
 */
const animation = (globalThis as { Animation?: typeof Animation }).Animation?.prototype;
if (animation) {
  const cancel = animation.cancel;
  animation.cancel = function (this: Animation): void {
    this.finished.catch(() => undefined);
    cancel.call(this);
  };
}

/**
 * A frame comes after the timers already due when it was asked for (#1800). A browser runs
 * animation frame callbacks when it updates the rendering, once a frame interval (HTML, "update
 * the rendering", https://html.spec.whatwg.org/multipage/webappapis.html#update-the-rendering),
 * and a 0 ms timer is a task that runs before that. happy-dom runs a frame as an immediate, ahead
 * of every timer: measured under the embedded bun, a 0 ms timer fired only after about 3,700
 * immediates (about 76 under Node), so two frames, or thirty, passed before it. A test waits frames
 * for what a component scheduled, and a component's `Task.Yield` is a 0 ms timer (TreeView's load
 * yields before it calls the loader), so `until(() => loads === 2)` failed whenever the frames held
 * too little work to last a millisecond: three of six runs of `test-runtime` on 2026-11-21.
 *
 * The frame now takes a 0 ms timer first, set when it is asked for, which runs after the timers
 * set before it, and is then handed to happy-dom's own, which draws it with its time. The timer is
 * the one taken when this file loads, so a spec that fakes the timers still has its frames drawn,
 * and one that fakes the frames as well replaces this pair while it does.
 */
const drawFrame = globalThis.requestAnimationFrame?.bind(globalThis);
const dropFrame = globalThis.cancelAnimationFrame?.bind(globalThis);
if (drawFrame && dropFrame) {
  const setTimer = globalThis.setTimeout.bind(globalThis);
  const clearTimer = globalThis.clearTimeout.bind(globalThis);
  // A frame asked for: the timer it waits for until that runs, then the frame happy-dom draws.
  const asked = new Map<number, { timer?: ReturnType<typeof setTimeout>; frame?: number }>();
  let last = 0;
  globalThis.requestAnimationFrame = (callback: FrameRequestCallback): number => {
    const id = ++last;
    const frame: { timer?: ReturnType<typeof setTimeout>; frame?: number } = {};
    frame.timer = setTimer(() => {
      frame.timer = undefined;
      frame.frame = drawFrame((time) => {
        asked.delete(id);
        callback(time);
      });
    }, 0);
    asked.set(id, frame);
    return id;
  };
  globalThis.cancelAnimationFrame = (id: number): void => {
    const frame = asked.get(id);
    if (!frame) return;
    asked.delete(id);
    if (frame.timer !== undefined) clearTimer(frame.timer);
    if (frame.frame !== undefined) dropFrame(frame.frame);
  };
}
