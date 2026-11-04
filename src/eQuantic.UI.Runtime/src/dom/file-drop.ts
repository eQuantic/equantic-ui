/**
 * The web half of the C# `DropTarget`: an area a person drops files onto.
 *
 * Only FILES count. A drag is told apart by what it carries (`dataTransfer.types` lists "Files"), so
 * text or a link dragged across the area is not an invitation and is left to the browser.
 *
 * "Over" ends when the drag has left every element it ENTERED inside the area, which is a set and not a
 * counter. Every child the pointer crosses fires its own enter and leave, and a counter drifts the
 * moment a re-render removes the element under the pointer, which then never reports its leave: the
 * area stayed lit after the files had gone. Keeping the entered elements, and forgetting the ones no
 * longer in the area, is what stays true across the re-render the "over" report itself causes. Whether
 * the area SAID it is over is kept beside the set, so the report is made once each way. Both live on the
 * area's element, which the reconciler keeps, rather than in a handler a re-render replaces.
 */

import type { EventHandler } from '../core/types';
import { PickedFile } from '../shared/picked-file';

interface Drag {
  entered: EventTarget[];
  over: boolean;
}

const drags = new WeakMap<Element, Drag>();

function dragOn(area: Element): Drag {
  let drag = drags.get(area);
  if (!drag) drags.set(area, (drag = { entered: [], over: false }));
  return drag;
}

/** Whether a drag carries files, which is the only drag a drop area answers. */
export function carriesFiles(event: Event): boolean {
  const types = (event as DragEvent).dataTransfer?.types;
  return !!types && Array.from(types).includes('Files');
}

/** The four handlers a drop area's element carries. */
export function dropHandlers(
  onDropped: ((files: PickedFile[]) => void) | null | undefined,
  onDragOverChanged: ((over: boolean) => void) | null | undefined,
  enabled: boolean,
): Record<string, EventHandler> {
  const enter = (event: Event): void => {
    if (!carriesFiles(event)) return;
    event.preventDefault();
    const area = event.currentTarget as Element;
    const drag = dragOn(area);
    drag.entered = drag.entered.filter((target) => area.contains(target as Node));
    if (event.target && !drag.entered.includes(event.target)) drag.entered.push(event.target);
    if (drag.over) return;
    drag.over = true;
    onDragOverChanged?.(true);
  };

  const over = (event: Event): void => {
    if (!carriesFiles(event)) return;
    // Accepting the dragover is what makes the element a drop target at all; a disabled area accepts
    // it too, and says "none", so the drop is refused here instead of falling through to the page.
    event.preventDefault();
    const transfer = (event as DragEvent).dataTransfer;
    if (transfer) transfer.dropEffect = enabled ? 'copy' : 'none';
  };

  const leave = (event: Event): void => {
    if (!carriesFiles(event)) return;
    const area = event.currentTarget as Element;
    const drag = dragOn(area);
    drag.entered = drag.entered.filter((target) => target !== event.target && area.contains(target as Node));
    if (drag.entered.length > 0 || !drag.over) return;
    drag.over = false;
    onDragOverChanged?.(false);
  };

  const drop = (event: Event): void => {
    if (!carriesFiles(event)) return;
    event.preventDefault();
    // The innermost area takes the files; an area around it must not take them a second time.
    event.stopPropagation();
    const area = event.currentTarget as Element;
    const wasOver = drags.get(area)?.over === true;
    drags.delete(area);
    if (wasOver) onDragOverChanged?.(false);
    if (!enabled || !onDropped) return;
    const files = Array.from((event as DragEvent).dataTransfer?.files ?? []).map((file) => PickedFile.fromFile(file));
    if (files.length > 0) onDropped(files);
  };

  return {
    dragenter: enter as EventHandler,
    dragover: over as EventHandler,
    dragleave: leave as EventHandler,
    drop: drop as EventHandler,
  };
}

let guarded = false;

/**
 * Once a page has shown a drop area, a file let go OUTSIDE every area is refused instead of opened. A
 * browser's own answer to a dropped file is to navigate to it, which replaces the page and loses
 * whatever the person had typed, for a drop that missed by a few pixels. Installed lazily by the
 * lowering, and idempotent, like the drag controllers beside it.
 */
export function installDropGuard(): void {
  if (guarded || typeof document === 'undefined') return;
  guarded = true;
  document.addEventListener('dragover', refuseMissedDrop);
  document.addEventListener('drop', refuseMissedDrop);
}

export function resetDropGuard(): void {
  if (typeof document === 'undefined') return;
  guarded = false;
  document.removeEventListener('dragover', refuseMissedDrop);
  document.removeEventListener('drop', refuseMissedDrop);
}

function refuseMissedDrop(event: Event): void {
  if (event.defaultPrevented || !carriesFiles(event)) return;
  event.preventDefault();
  const transfer = (event as DragEvent).dataTransfer;
  if (transfer) transfer.dropEffect = 'none';
}
