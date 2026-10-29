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
