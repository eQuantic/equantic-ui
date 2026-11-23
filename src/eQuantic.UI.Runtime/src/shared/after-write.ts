/**
 * The one moment after a pass is WRITTEN, for the work that reads what it wrote.
 *
 * A pass ends (`exitPass`) before the render manager writes the tree it produced, so anything the end of a
 * pass does against the document reads the DOM the pass before had written. The modal focus traps did, and a
 * dialog a render opened took no focus until a later pass came (#1812); the camera streams did, and a preview a
 * render showed got its stream only on a later pass (#1830). Both are queued here instead, and run in the
 * microtask after the write, the moment the in-view observers attach at.
 *
 * A task queued twice before the flush runs once: what the end of a pass queues is a reconciliation of the whole
 * document, and running it again finds nothing new. A task that throws costs itself alone: the others still run,
 * and what it threw is reported as an uncaught error is.
 *
 * The other commits that wait for the write (the in-view observers, the scroll and table viewports, the canvases,
 * the anchor offset, the scrolled headers) still schedule their own; they can join this moment one by one.
 */

const queued = new Set<() => void>();
let flushing = false;

/** Runs `task` in the microtask after the write of the pass that is ending. */
export function afterWrite(task: () => void): void {
  if (typeof document === 'undefined') return;
  queued.add(task);
  if (flushing) return;
  flushing = true;
  if (typeof queueMicrotask === 'function') queueMicrotask(flush);
  else void Promise.resolve().then(flush);
}

/** Drops `task` if it is still queued: a reset that must not see a reconciliation queued before it. */
export function cancelAfterWrite(task: () => void): void {
  queued.delete(task);
}

function flush(): void {
  flushing = false;
  const tasks = [...queued];
  queued.clear();
  for (const task of tasks) {
    try {
      task();
    } catch (error) {
      report(error);
    }
  }
}

/** What a task threw, where an uncaught error goes: the window's own report, or the console without one. */
function report(error: unknown): void {
  const reporter = (globalThis as { reportError?: (error: unknown) => void }).reportError;
  if (typeof reporter === 'function') reporter(error);
  else console.error(error);
}
