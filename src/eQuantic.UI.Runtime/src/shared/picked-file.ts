/**
 * The C# `PickedFile`: a file a person chose, over the `File` the browser handed the page.
 *
 * Unlike the shapes in primitive-values.ts this one carries behaviour, because its content is the
 * browser's: the page holds a handle and none of the bytes until it asks for them. Choosing a 250 MB
 * model reads nothing, and `readAllBytesAsync` reads the whole file, which is for one small enough to
 * hold.
 *
 * In C# the type is abstract and each target realizes it. Here there is one target, so the twin IS
 * the realization, and a page never constructs one: the picker and the drop area do.
 */

import type { CancellationToken } from '../utils/cancellation';

export class PickedFile {
  /** What a file whose content the browser could not name is: bytes, nothing more. */
  static readonly unknownMimeType = 'application/octet-stream';

  readonly name: string;
  readonly mimeType: string;
  /** A C# `long`, which the runtime holds as a bigint, so arithmetic a page writes on it stays exact. */
  readonly size: bigint;
  /** The browser's handle to the content, which an upload reads a part at a time. */
  readonly content: Blob;

  constructor(name: string, mimeType: string, size: bigint, content: Blob) {
    this.name = name;
    this.mimeType = mimeType || PickedFile.unknownMimeType;
    this.size = size;
    this.content = content;
  }

  /** The twin of what a native realization does with its picker's answer. */
  static fromFile(file: File): PickedFile {
    return new PickedFile(file.name, file.type, BigInt(file.size), file);
  }

  async readAllBytesAsync(cancellationToken?: CancellationToken | null): Promise<Uint8Array> {
    cancellationToken?.throwIfCancellationRequested();
    const bytes = new Uint8Array(await this.content.arrayBuffer());
    cancellationToken?.throwIfCancellationRequested();
    return bytes;
  }

  /** Decoded as UTF-8, which is what a Blob's own `text()` decodes as. */
  async readAllTextAsync(cancellationToken?: CancellationToken | null): Promise<string> {
    cancellationToken?.throwIfCancellationRequested();
    const text = await this.content.text();
    cancellationToken?.throwIfCancellationRequested();
    return text;
  }
}
