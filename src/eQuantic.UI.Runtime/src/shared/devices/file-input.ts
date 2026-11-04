/**
 * A file input, clicked for the person: the one way a page may ask for files. The photo library and
 * the file picker both ask through it, and differ only in what they accept.
 *
 * It has to be IN the document for Safari to open the sheet at all, and it is removed afterwards, or a
 * page that picks often grows a pile of dead inputs.
 *
 * And the browser opens it only in answer to a person's gesture. Outside one it opens nothing and
 * reports nothing either, so the promise would wait for ever: where the browser says whether a gesture
 * is live (`navigator.userActivation`), a call outside one answers "nothing chosen" at once.
 */

import type { CancellationToken } from '../../utils/cancellation';

export interface FileInputOptions {
  /** The input's `accept`: what the sheet offers first. Absent means anything. */
  accept?: string;
  multiple: boolean;
  /** Stops waiting for the person, who may still be looking at the sheet. */
  cancellationToken?: CancellationToken | null;
}

export function chooseFiles(options: FileInputOptions): Promise<File[]> {
  const activation = (navigator as Navigator & { userActivation?: { isActive: boolean } }).userActivation;
  if (activation && !activation.isActive) return Promise.resolve([]);
  if (options.cancellationToken?.isCancellationRequested) return Promise.resolve([]);

  return new Promise((resolve) => {
    const input = document.createElement('input');
    input.type = 'file';
    if (options.accept) input.accept = options.accept;
    input.multiple = options.multiple;
    input.style.position = 'fixed';
    input.style.left = '-10000px';
    document.body.appendChild(input);

    let settled = false;
    const finish = (files: File[]) => {
      if (settled) return;
      settled = true;
      registration?.dispose();
      window.removeEventListener('focus', refocused);
      input.remove();
      resolve(files);
    };
    const registration = options.cancellationToken?.register(() => finish([]));

    input.addEventListener('change', () => finish(Array.from(input.files ?? [])));
    // CANCEL is not reliably reported: 'cancel' is recent and absent in older Safari, so the window
    // regaining focus is the fallback. Focus comes back BEFORE the change event, and a large choice
    // or one on a network drive can take a while to become a file list, so the fallback waits a
    // second and then takes whatever the input holds: a late choice is still the person's choice.
    input.addEventListener('cancel', () => finish([]));
    const refocused = () => setTimeout(() => finish(Array.from(input.files ?? [])), 1000);
    window.addEventListener('focus', refocused, { once: true });

    input.click();
  });
}
