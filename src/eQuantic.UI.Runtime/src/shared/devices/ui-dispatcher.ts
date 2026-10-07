/**
 * The web realization of C# `IUiDispatcher` — and on this target it exists to say that there is
 * nothing to marshal.
 *
 * JavaScript runs the page on one thread. Nothing can mutate a component's fields while the tree is
 * being built, because nothing else is running: that is why `SetState` stays inline on the web while
 * the native hosts post through a queue. So `isOnUiThread` is simply true, and it is true honestly
 * rather than as a stub — the code asking is right to believe it.
 *
 * `post` still defers, because the contract says so and because deferring is occasionally what a
 * page wants (let the current task finish, then continue). A microtask, not a timer: it runs before
 * the next paint, so state posted during an event handler is on screen in the same frame.
 */
export class WebUiDispatcher {
  get isOnUiThread(): boolean {
    return true;
  }

  post(work: () => void): void {
    if (typeof queueMicrotask === 'function') {
      queueMicrotask(work);
      return;
    }
    // Older embeddings (and some test doubles) have no microtask queue; a resolved promise is the
    // same queue by another name, and a timer is the last resort rather than the default.
    void Promise.resolve().then(work);
  }
}

/**
 * C#'s `UiDispatcher`, the dispatcher of the process: null on the web, as its own documentation says
 * it is wherever there is only one thread. A twin that asks for it (the code engine's completion,
 * applying a provider's answer on the thread the list is read on) reads null and runs the work at
 * once, which is exactly what one thread means.
 */
export class UiDispatcher {
  static current: { readonly isOnUiThread: boolean; post(work: () => void): void } | null = null;
}
