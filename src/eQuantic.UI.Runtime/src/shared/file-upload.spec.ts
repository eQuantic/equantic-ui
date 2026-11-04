/**
 * The FileUpload component as the browser runs it, over stand-ins for the two capabilities it asks
 * for: a picker that answers with chosen files and an IUploads whose uploads a test moves by hand. What
 * the component promises is asserted here: a row per file with its progress and its controls, a refusal
 * shown in its row before anything is sent, every arrival handed to the page once, and an area drawn
 * disabled where it cannot send.
 */

import { afterEach, describe, expect, it } from 'vitest';
import { configureServices, resetServiceProvider } from '../core/service-provider';
import { FileUpload } from './components/FileUpload';
import { PickedFile } from './picked-file';
import { FileFilter, UploadedFile, UploadPolicy } from './primitive-values';
import { Upload } from './upload';

const nextFrame = () =>
  new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));

const models = new UploadPolicy('models', [new FileFilter('3D models', 'stl', 'obj')], 250n * 1024n * 1024n);

const file = (name: string, size: number) =>
  PickedFile.fromFile(new File([new Uint8Array(size)], name, { type: 'model/stl' }));

/** An Upload whose state the test sets, and which tells its subscribers as the real one does. */
class HeldUpload extends Upload {
  paused = false;
  cancelled = false;

  move(state: 'sending' | 'completed' | 'failed', sent = 0n, error: string | null = null): void {
    Object.assign(this as unknown as Record<string, unknown>, { _state: state, _sent: sent, _error: error });
    if (state === 'completed')
      Object.assign(this as unknown as Record<string, unknown>, {
        _result: new UploadedFile('f00d', this.file.name, this.file.mimeType, this.file.size, this.policy.name),
      });
    (this as unknown as { notify(): void }).notify();
  }

  override pause(): void {
    this.paused = true;
  }

  override cancel(): void {
    this.cancelled = true;
  }
}

class HeldUploads {
  readonly started: HeldUpload[] = [];
  constructor(readonly isAvailable = true) {}

  start(picked: PickedFile, policy: UploadPolicy): Upload {
    const refusal = policy.refusalOf(picked.name, picked.size);
    const upload = new HeldUpload(picked, policy);
    if (refusal === null) this.started.push(upload);
    else Object.assign(upload as unknown as Record<string, unknown>, { _state: 'failed', _error: refusal });
    return upload;
  }
}

class Picker {
  asked: unknown[] = [];
  constructor(
    readonly answer: PickedFile[],
    readonly isAvailable = true,
  ) {}

  async pickFilesAsync(filters: unknown): Promise<PickedFile[]> {
    this.asked.push(filters);
    return this.answer;
  }

  async pickFileAsync(filters: unknown): Promise<PickedFile | null> {
    this.asked.push(filters);
    return this.answer[0] ?? null;
  }
}

function mount(component: FileUpload, picker: Picker | null, uploads: HeldUploads | null) {
  resetServiceProvider();
  configureServices((services) => {
    if (picker) services.registerInstance('IFilePicker', picker);
    if (uploads) services.registerInstance('IUploads', uploads);
  });
  const container = document.createElement('div');
  document.body.appendChild(container);
  component.mount(container);
  return container;
}

const choose = (container: HTMLElement) =>
  [...container.querySelectorAll('button')].find((button) => button.textContent?.includes('Choose')) as HTMLButtonElement;

describe('FileUpload', () => {
  afterEach(() => {
    document.body.innerHTML = '';
    resetServiceProvider();
  });

  it('says what the policy accepts, and asks the picker for its formats', async () => {
    const picker = new Picker([]);
    const container = mount(new FileUpload(models, () => {}), picker, new HeldUploads());

    expect(container.textContent).toContain('Drop files here, or choose them');
    expect(container.textContent).toContain('STL, OBJ, up to 250 MB');
    choose(container).click();
    await nextFrame();
    expect(picker.asked).toEqual([models.filters]);
  });

  it('shows a row per file with its progress, and hands the page each arrival once', async () => {
    const uploads = new HeldUploads();
    const arrived: UploadedFile[] = [];
    const container = mount(
      new FileUpload(models, (uploaded) => arrived.push(uploaded)),
      new Picker([file('bracket.stl', 2048), file('lid.obj', 10)]),
      uploads,
    );

    choose(container).click();
    await nextFrame();
    expect(uploads.started.map((upload) => upload.file.name)).toEqual(['bracket.stl', 'lid.obj']);
    expect(container.textContent).toContain('bracket.stl');
    expect(container.querySelectorAll('[role="progressbar"]')).toHaveLength(2);

    uploads.started[0].move('sending', 1024n);
    await nextFrame();
    expect(container.textContent).toContain('1 KB of 2 KB');
    const bar = container.querySelector('[role="progressbar"]') as HTMLElement;
    expect(bar.getAttribute('aria-label')).toBe('Uploading bracket.stl');
    expect(bar.getAttribute('aria-valuenow')).toBe('0.5');
    [...container.querySelectorAll('button')].find((button) => button.textContent === 'Pause')!.click();
    expect(uploads.started[0].paused).toBe(true);

    uploads.started[0].move('completed', 2048n);
    uploads.started[0].move('completed', 2048n);
    await nextFrame();
    expect(arrived.map((uploaded) => uploaded.name)).toEqual(['bracket.stl']);
    expect(container.textContent).toContain('Uploaded');
    expect(container.querySelector('[aria-live]')?.textContent).toContain('bracket.stl uploaded.');
  });

  it('refuses in the row, in the policy words, a file it does not accept', async () => {
    const uploads = new HeldUploads();
    const container = mount(new FileUpload(models, () => {}), new Picker([file('setup.exe', 4)]), uploads);

    choose(container).click();
    await nextFrame();

    expect(uploads.started).toHaveLength(0);
    expect(container.textContent).toContain('Only .stl, .obj files are accepted here.');
    expect(container.querySelector('[aria-live]')?.textContent).toContain(
      'setup.exe was not uploaded: Only .stl, .obj files are accepted here.',
    );
  });

  it('takes one file when it is not multiple', async () => {
    const uploads = new HeldUploads();
    const container = mount(
      new FileUpload(models, () => {}, false),
      new Picker([file('bracket.stl', 1), file('lid.obj', 1)]),
      uploads,
    );

    expect(choose(container).textContent).toBe('Choose a file');
    choose(container).click();
    await nextFrame();
    expect(uploads.started.map((upload) => upload.file.name)).toEqual(['bracket.stl']);
  });

  it('draws the same area, disabled, where there is nothing to send with', () => {
    const container = mount(new FileUpload(models, () => {}), new Picker([]), null);

    expect(container.textContent).toContain('Drop files here, or choose them');
    expect(choose(container).disabled).toBe(true);
    expect(container.querySelector('[data-eq-drop]')?.getAttribute('data-eq-drop')).toBe('off');
  });

  it('pauses what is still on its way when it leaves the screen', async () => {
    const uploads = new HeldUploads();
    const component = new FileUpload(models, () => {});
    const container = mount(component, new Picker([file('bracket.stl', 10)]), uploads);
    choose(container).click();
    await nextFrame();

    // What the lifecycle calls when the component's position leaves the tree (lifecycle.spec.ts proves
    // the call; this, what the component does with it).
    component.onUnmount();

    expect(uploads.started[0].paused).toBe(true);
  });
});
