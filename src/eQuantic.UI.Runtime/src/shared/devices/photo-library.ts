/**
 * The browser's answer to "let the user choose a picture": a file input, clicked for them.
 *
 * The same contract the native shells satisfy — a page written once takes an `IPhotoLibrary`
 * through its constructor and never learns which of the four answered. Here there is nothing to ask
 * permission FOR: a file input hands over exactly what was chosen and the page cannot see anything
 * else, which is the same guarantee the modern phone pickers give.
 */

/** Encoded bytes as the device produced them — the C# `ImageData`, in its transpiled shape. */
export interface PickedImage {
  bytes: Uint8Array;
  mimeType: string;
  width: number;
  height: number;
  name: string | null;
}

export class WebPhotoLibrary {
  /** How long to wait for the decoder before handing the picture over unmeasured. */
  private static readonly MeasureTimeoutMs = 1500;

  get isAvailable(): boolean {
    return typeof document !== 'undefined';
  }

  async getPermissionAsync(): Promise<string> {
    // 'granted': there is nothing to ask for. The file input IS the grant, given per file, at the
    // moment the user picks — which is stronger than a permission and needs no prompt.
    return 'granted';
  }

  async pickImageAsync(): Promise<PickedImage | null> {
    const picked = await this.pickImagesAsync(1);
    return picked.length > 0 ? picked[0] : null;
  }

  async pickImagesAsync(limit = 0): Promise<PickedImage[]> {
    if (!this.isAvailable) return [];

    const files = await this.chooseFiles(limit !== 1);
    const chosen = limit > 0 ? files.slice(0, limit) : files;
    return Promise.all(chosen.map((file) => this.read(file)));
  }

  /**
   * A file input, clicked. It has to be IN the document for Safari to open the sheet at all, and it
   * has to be removed afterwards or a page that picks often grows a pile of dead inputs.
   */
  private chooseFiles(multiple: boolean): Promise<File[]> {
    return new Promise((resolve) => {
      const input = document.createElement('input');
      input.type = 'file';
      input.accept = 'image/*';
      input.multiple = multiple;
      input.style.position = 'fixed';
      input.style.left = '-10000px';
      document.body.appendChild(input);

      let settled = false;
      const finish = (files: File[]) => {
        if (settled) return;
        settled = true;
        input.remove();
        resolve(files);
      };

      input.addEventListener('change', () => finish(Array.from(input.files ?? [])));
      // CANCEL is not reliably reported: 'cancel' is recent and absent in older Safari, so the
      // window regaining focus is the fallback. A frame of delay lets a real 'change' win the race —
      // focus comes back before the change event fires.
      input.addEventListener('cancel', () => finish([]));
      window.addEventListener(
        'focus',
        () => setTimeout(() => finish([]), 300),
        { once: true },
      );

      input.click();
    });
  }

  private async read(file: File): Promise<PickedImage> {
    const bytes = new Uint8Array(await file.arrayBuffer());
    const { width, height } = await this.measure(file);
    return {
      bytes,
      mimeType: file.type || 'image/jpeg',
      width,
      height,
      name: file.name || null,
    };
  }

  /**
   * Dimensions from the browser's own decoder rather than from the header: it already has one, it
   * knows every format the platform supports, and it costs an object URL. A failure answers zero —
   * not knowing how big a picture is has never been a reason to refuse to hand it over.
   *
   * And it GIVES UP. A decoder that answers neither onload nor onerror leaves the promise pending
   * for ever, which means the picture the user chose never arrives and the button that opened the
   * picker stays dead. The size is a nicety; the picture is the point.
   */
  private measure(file: File): Promise<{ width: number; height: number }> {
    return new Promise((resolve) => {
      const unknown = { width: 0, height: 0 };
      let settled = false;
      const finish = (size: { width: number; height: number }) => {
        if (settled) return;
        settled = true;
        URL.revokeObjectURL(url);
        resolve(size);
      };

      const url = URL.createObjectURL(file);
      const image = new Image();
      image.onload = () => finish({ width: image.naturalWidth, height: image.naturalHeight });
      image.onerror = () => finish(unknown);
      setTimeout(() => finish(unknown), WebPhotoLibrary.MeasureTimeoutMs);
      image.src = url;
    });
  }
}
