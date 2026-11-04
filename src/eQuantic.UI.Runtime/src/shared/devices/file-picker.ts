/**
 * The browser's answer to `IFilePicker`: the same file input the photo library opens, accepting what
 * the filters name, and answering with `PickedFile`s over what the person chose.
 *
 * The filters become the input's `accept`, which a browser offers as the default and lets a person
 * widen. So what arrives is what the person chose, and the app checks it where it is received, as the
 * upload policy does on the server.
 */

import type { CancellationToken } from '../../utils/cancellation';
import { PickedFile } from '../picked-file';
import type { FileFilter } from '../primitive-values';
import { chooseFiles } from './file-input';

export class WebFilePicker {
  get isAvailable(): boolean {
    return typeof document !== 'undefined';
  }

  async pickFileAsync(
    filters?: readonly FileFilter[] | null,
    cancellationToken?: CancellationToken | null,
  ): Promise<PickedFile | null> {
    const picked = await this.choose(filters, false, cancellationToken);
    return picked.length > 0 ? picked[0] : null;
  }

  pickFilesAsync(
    filters?: readonly FileFilter[] | null,
    cancellationToken?: CancellationToken | null,
  ): Promise<PickedFile[]> {
    return this.choose(filters, true, cancellationToken);
  }

  // Not async before the input is clicked: the click has to happen inside the gesture that called.
  private async choose(
    filters: readonly FileFilter[] | null | undefined,
    multiple: boolean,
    cancellationToken: CancellationToken | null | undefined,
  ): Promise<PickedFile[]> {
    if (!this.isAvailable) return [];
    const files = await chooseFiles({ accept: acceptOf(filters), multiple, cancellationToken });
    return files.map((file) => PickedFile.fromFile(file));
  }
}

/**
 * The input's `accept` for a list of filters: every extension of every filter, dotted once. A filter
 * with no extensions means anything, and so does no filter at all.
 */
export function acceptOf(filters: readonly FileFilter[] | null | undefined): string | undefined {
  if (!filters || filters.length === 0) return undefined;
  const extensions = new Set<string>();
  for (const filter of filters) {
    if (filter.extensions.length === 0) return undefined;
    for (const extension of filter.extensions) {
      const bare = extension.replace(/^\.+/, '').toLowerCase();
      if (bare) extensions.add('.' + bare);
    }
  }
  return extensions.size > 0 ? [...extensions].join(',') : undefined;
}
