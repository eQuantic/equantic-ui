/**
 * The development error overlay: a full-screen card for an exception nothing caught, in the only
 * language the developer wrote. Every frame of the call stack is named by whose code it ran
 * (`frame-owner.ts`): the app's C#, the SDK's own C# or the runtime's module, never a JavaScript
 * file's line and column (#1108). An error raised inside the SDK says the fault is the SDK's and
 * hands the developer a report to copy, as a Flutter assertion asks to be filed against Flutter.
 */

import { remapFramesToCSharp, remapStackTrace, RemappedFrame } from './stack-remapper';
import { RawSourceMap } from './source-map';
import { describeFrame, nameFrames, NamedFrame } from './frame-owner';

const isDev = () => typeof window !== 'undefined' && window.__EQ_DEV__ === true;

/** Where an error raised inside the SDK is reported. */
const ISSUES_URL = 'https://github.com/eQuantic/equantic-ui/issues/new';

/** What was raised: an exception nothing caught, or one an asynchronous call raised that nothing
 * awaited (the browser's unhandled rejection). */
type ErrorKind = 'exception' | 'unobserved';

interface ErrorInfo {
  kind: ErrorKind;
  message: string;
  stack?: string;
}

export class ErrorOverlay {
  private overlay: HTMLDivElement | null = null;
  private errors: ErrorInfo[] = [];
  /** The render a stack being named belongs to: an error shown while an earlier one's maps load
   * replaces it, and the earlier answer is dropped. */
  private rendering = 0;

  /** `runtimeUrl` is the served runtime's own URL, where the overlay itself ships: a frame run from
   * it is the SDK's. */
  constructor(private readonly runtimeUrl: string = import.meta.url) {}

  show(error: ErrorInfo) {
    if (!isDev()) return;
    this.errors.push(error);
    this.render();
  }

  clear() {
    this.errors = [];
    document.removeEventListener('keydown', this.onKey);
    if (this.overlay) {
      this.overlay.remove();
      this.overlay = null;
    }
  }

  private readonly onKey = (event: KeyboardEvent) => {
    if (event.key === 'Escape') this.clear();
  };

  private render() {
    if (!this.overlay) {
      this.overlay = document.createElement('div');
      this.overlay.id = 'equantic-error-overlay';
      document.body.appendChild(this.overlay);
      document.addEventListener('keydown', this.onKey);
    }

    const error = this.errors[this.errors.length - 1];
    const rendering = ++this.rendering;

    // The call stack is drawn once its frames are named, never before: what the browser hands over is
    // JavaScript, and a developer who wrote C# is not shown it even while the maps load.
    this.overlay.innerHTML = `
      <style>${STYLES}</style>
      <div class="card">
        <div class="header">
          <div class="header-title"><i>!</i><span>Unhandled Runtime Error</span></div>
          <button class="close-btn" data-eq-dismiss>Dismiss (Esc)</button>
        </div>
        <div class="content">
          <div class="error-message">${this.escapeHtml(error.message)}</div>
          ${error.kind === 'unobserved' ? '<div class="error-note">Raised by an asynchronous call that nothing awaited.</div>' : ''}
          <div id="eq-fault"></div>
          <div id="eq-source"></div>
          <div class="section-label">Call Stack</div>
          <div class="stack-trace" id="eq-stack-trace"><span class="stack-pending">Reading the call stack…</span></div>
        </div>
        <div class="footer">Dev Mode Overlay &bull; Fix the error above to resume.</div>
      </div>
    `;
    this.overlay.querySelector('[data-eq-dismiss]')?.addEventListener('click', () => this.clear());

    void this.nameStack(error, rendering);
  }

  private async nameStack(error: ErrorInfo, rendering: number): Promise<void> {
    const frames = await this.named(error.stack ?? '');
    if (!this.overlay || rendering !== this.rendering) return;

    const stackEl = this.overlay.querySelector('#eq-stack-trace');
    if (stackEl) {
      stackEl.innerHTML = frames.length
        ? frames.map((frame) => this.formatFrame(frame)).join('')
        : '<span class="stack-pending">The browser recorded no call stack.</span>';
    }

    // The throwing frame decides whose fault it is.
    if (frames[0]?.owner === 'sdk') {
      const faultEl = this.overlay.querySelector('#eq-fault');
      if (faultEl) this.renderFault(faultEl, error, frames);
    }

    // The app's C# around the first frame that carries it; the SDK's maps carry no text.
    const top = frames.find((frame) => frame.csharp && frame.sourceContent);
    const sourceEl = this.overlay.querySelector('#eq-source');
    if (top && sourceEl) {
      sourceEl.innerHTML =
        `<div class="section-label">Source Context <span class="section-file">${this.escapeHtml(top.place)}:${top.line}</span></div>` +
        `<div class="code-block">${this.renderCSharpSnippet(top)}</div>`;
    }
  }

  /** The stack's frames named by their owners: through the maps, and without them when reading the
   * maps failed, so a frame is named by its owner rather than left at its JavaScript position. */
  private async named(stack: string): Promise<NamedFrame[]> {
    let frames: RemappedFrame[];
    try {
      // Hop one walks the bundle's map: an app module's leads to its C#, the runtime's to the SDK's C#
      // or its module. Hop two walks eqc's stage-one map for a frame left in a TS intermediate.
      frames = await remapStackTrace(stack, (url) => this.fetchSourceMap(url));
      frames = await remapFramesToCSharp(frames, (name) => this.fetchStageOneMap(name));
    } catch {
      frames = await remapStackTrace(stack, async () => null);
    }
    return nameFrames(frames, this.runtimeUrl, window.location.origin);
  }

  private renderFault(target: Element, error: ErrorInfo, frames: NamedFrame[]) {
    const call = frames.find((frame) => frame.owner === 'app');
    const where = call
      ? `It was raised while running your code at <b>${this.escapeHtml(this.place(call))}</b>: check that call first. If it is right, the fault is the SDK's.`
      : "No code of your app is on the call stack: the fault is the SDK's, not your app's.";
    target.innerHTML = `
      <div class="fault">
        <div class="fault-title">This error was raised inside eQuantic.UI</div>
        <p>${where}</p>
        <p>Please report it: copy the report below and open an issue for the eQuantic.UI team.</p>
        <div class="fault-actions">
          <button class="close-btn" data-eq-copy>Copy report</button>
          <a class="close-btn" href="${ISSUES_URL}" target="_blank" rel="noopener noreferrer">Open an issue</a>
        </div>
        <details><summary>Report</summary><textarea readonly class="report" data-eq-report>${this.escapeHtml(this.report(error, frames))}</textarea></details>
      </div>
    `;
    const report = target.querySelector<HTMLTextAreaElement>('[data-eq-report]');
    const copy = target.querySelector<HTMLButtonElement>('[data-eq-copy]');
    copy?.addEventListener('click', () => {
      const text = report?.value ?? '';
      const copied = () => {
        copy.textContent = 'Copied';
      };
      // Where the clipboard refuses (a page that is not a secure context), the report is selected for
      // a copy by hand.
      const byHand = () => {
        report?.closest('details')?.setAttribute('open', '');
        report?.select();
      };
      if (navigator.clipboard) navigator.clipboard.writeText(text).then(copied, byHand);
      else byHand();
    });
  }

  /** What the developer pastes into an issue: the error, the SDK it was raised in, where, and every
   * frame named as the overlay names it. The page's path and nothing after it: a query can carry what
   * the page was handed. */
  private report(error: ErrorInfo, frames: NamedFrame[]): string {
    return [
      'eQuantic.UI error report',
      `SDK: ${window.__EQ_CONFIG?.sdkVersion ?? 'unknown'}`,
      `Page: ${window.location.pathname}`,
      `Browser: ${navigator.userAgent}`,
      '',
      `Unhandled Runtime Error${error.kind === 'unobserved' ? ' (an asynchronous call nothing awaited)' : ''}: ${error.message}`,
      '',
      ...frames.map((frame) => `  at ${describeFrame(frame, true)}${OWNER_TAGS[frame.owner]}`),
    ].join('\n');
  }

  private place(frame: NamedFrame): string {
    return frame.csharp && frame.line !== null ? `${frame.place}:${frame.line}` : frame.place;
  }

  private formatFrame(frame: NamedFrame): string {
    const func = this.escapeHtml(frame.func ?? '<anonymous>');
    const line =
      frame.csharp && frame.line !== null ? `:<span class="stack-pos">${frame.line}</span>` : '';
    const tag = OWNER_TAGS[frame.owner]
      ? `<span class="owner-tag">${OWNER_TAGS[frame.owner].trim()}</span>`
      : '';
    return (
      `<span class="stack-line ${frame.owner}">at <span class="eq-hl-func">${func}</span> ` +
      `(<span class="stack-file">${this.escapeHtml(frame.place)}</span>${line})${tag}</span>`
    );
  }

  private async fetchSourceMap(jsUrl: string): Promise<RawSourceMap | null> {
    try {
      // Bun emits "<file>.js.map" next to the bundle, and the Server answers the runtime's own at
      // /_equantic/runtime.js.map in Development. The frame's URL carries the cache-busting query
      // (`?v=…`), and appending ".map" AFTER it asked for a file that does not exist — which is why
      // the very first live run of this overlay showed JS frames: the map fetch had been 404ing
      // quietly since the day it was written.
      const clean = jsUrl.split('?')[0];
      // A frame of code the browser evaluated (`<anonymous>`) or an extension ran has no map to ask for.
      if (!/^https?:\/\//.test(clean)) return null;
      const response = await fetch(`${clean}.map`);
      if (!response.ok) return null;
      return (await response.json()) as RawSourceMap;
    } catch {
      return null;
    }
  }

  private async fetchStageOneMap(tsName: string): Promise<RawSourceMap | null> {
    try {
      // Development-only endpoint beside /hmr; 404s in production and the frame keeps its TS name.
      const response = await fetch(`/_equantic/src-map/${encodeURIComponent(tsName)}.map`);
      if (!response.ok) return null;
      return (await response.json()) as RawSourceMap;
    } catch {
      return null;
    }
  }

  private renderCSharpSnippet(frame: NamedFrame): string {
    const content = frame.sourceContent ?? '';
    const lines = content.split('\n');
    const target = frame.line ?? 1; // 1-based
    const start = Math.max(1, target - 2);
    const end = Math.min(lines.length, target + 2);

    const out: string[] = [];
    for (let n = start; n <= end; n++) {
      const isTarget = n === target;
      const gutter = String(n).padStart(4, ' ');
      const text = this.escapeHtml(lines[n - 1] ?? '');
      out.push(
        `<div class="stack-line"${isTarget ? ' style="background:rgba(229,62,62,0.15)"' : ''}>` +
          `<span class="stack-pos">${gutter}</span>  ${isTarget ? '<span class="eq-hl-keyword">▶</span> ' : '  '}${text}` +
          `</div>`,
      );
    }
    return out.join('');
  }

  private escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }
}

/** What follows a frame of the SDK and one outside the app, in the call stack and the report. */
const OWNER_TAGS: Record<NamedFrame['owner'], string> = {
  app: '',
  sdk: ' [eQuantic.UI]',
  external: ' [outside the app]',
};

const STYLES = `
#equantic-error-overlay {
  position: fixed; top: 0; left: 0; right: 0; bottom: 0;
  background: rgba(10, 10, 12, 0.8);
  backdrop-filter: blur(20px); -webkit-backdrop-filter: blur(20px);
  color: #f8fafc; z-index: 999999;
  display: flex; align-items: center; justify-content: center;
  font-family: 'Inter', -apple-system, system-ui, sans-serif;
  padding: 40px;
}
#equantic-error-overlay .card {
  background: rgba(30, 30, 35, 0.7);
  border: 1px solid rgba(255, 255, 255, 0.1); border-radius: 20px;
  width: 100%; max-width: 900px; max-height: 90vh;
  display: flex; flex-direction: column;
  box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5);
  overflow: hidden; animation: eq-fade-in 0.3s ease-out;
}
@keyframes eq-fade-in {
  from { opacity: 0; transform: translateY(20px); }
  to { opacity: 1; transform: translateY(0); }
}
#equantic-error-overlay .header {
  padding: 24px 32px; border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  display: flex; justify-content: space-between; align-items: center;
  background: rgba(229, 62, 62, 0.1);
}
#equantic-error-overlay .header-title {
  display: flex; align-items: center; gap: 12px;
  font-weight: 700; font-size: 18px; color: #feb2b2;
}
#equantic-error-overlay .header-title i {
  font-style: normal; background: #e53e3e; color: white;
  width: 24px; height: 24px; display: flex; align-items: center; justify-content: center;
  border-radius: 6px; font-size: 14px;
}
#equantic-error-overlay .close-btn {
  background: rgba(255, 255, 255, 0.05); border: 1px solid rgba(255, 255, 255, 0.1);
  color: #cbd5e1; padding: 8px 16px; border-radius: 8px; cursor: pointer;
  font-size: 13px; font-weight: 500; transition: all 0.2s; text-decoration: none;
}
#equantic-error-overlay .close-btn:hover { background: rgba(255, 255, 255, 0.1); color: white; }
#equantic-error-overlay .content { flex: 1; overflow: auto; padding: 32px; }
#equantic-error-overlay .error-message {
  font-size: 20px; font-weight: 600; margin-bottom: 24px; line-height: 1.4;
  color: #fff; word-break: break-word;
}
#equantic-error-overlay .error-note { margin: -16px 0 24px; color: #cbd5e1; font-size: 14px; }
#equantic-error-overlay .fault {
  border: 1px solid rgba(251, 191, 36, 0.35); background: rgba(251, 191, 36, 0.08);
  border-radius: 12px; padding: 16px 20px; margin-bottom: 24px; font-size: 14px; line-height: 1.5;
}
#equantic-error-overlay .fault-title { font-weight: 700; color: #fcd34d; margin-bottom: 4px; }
#equantic-error-overlay .fault p { margin: 6px 0; color: #e2e8f0; }
#equantic-error-overlay .fault-actions { display: flex; gap: 8px; margin: 12px 0 8px; }
#equantic-error-overlay .report {
  width: 100%; min-height: 160px; margin-top: 8px; box-sizing: border-box;
  background: #0f172a; color: #cbd5e1; border: 1px solid rgba(255, 255, 255, 0.1); border-radius: 8px;
  font-family: 'JetBrains Mono', 'Fira Code', 'Menlo', monospace; font-size: 12px; padding: 12px;
}
#equantic-error-overlay .section-label {
  text-transform: uppercase; font-size: 11px; font-weight: 700; letter-spacing: 0.1em;
  color: #94a3b8; margin-bottom: 12px; display: flex; align-items: center; gap: 8px;
}
#equantic-error-overlay .section-file { text-transform: none; letter-spacing: normal; color: #60a5fa; }
#equantic-error-overlay .section-label::after {
  content: ''; flex: 1; height: 1px; background: rgba(255, 255, 255, 0.05);
}
#equantic-error-overlay .code-block {
  background: #0f172a; border-radius: 12px; padding: 20px; margin-bottom: 24px;
  font-family: 'JetBrains Mono', 'Fira Code', 'Menlo', monospace; font-size: 13px; line-height: 1.6;
  overflow-x: auto; border: 1px solid rgba(255, 255, 255, 0.05); white-space: pre;
}
#equantic-error-overlay .stack-trace {
  color: #94a3b8; font-family: 'JetBrains Mono', 'Fira Code', 'Menlo', monospace;
  font-size: 12px; line-height: 1.6; white-space: pre-wrap; word-break: break-all;
}
#equantic-error-overlay .stack-line { display: block; padding: 4px 8px; border-radius: 4px; margin-bottom: 2px; }
#equantic-error-overlay .stack-line:hover { background: rgba(255, 255, 255, 0.05); color: #cbd5e1; }
#equantic-error-overlay .stack-line.sdk, #equantic-error-overlay .stack-line.external { opacity: 0.7; }
#equantic-error-overlay .stack-pending { font-style: italic; }
#equantic-error-overlay .owner-tag {
  margin-left: 8px; font-size: 10px; padding: 1px 6px; border-radius: 4px;
  background: rgba(255, 255, 255, 0.08); color: #cbd5e1;
}
#equantic-error-overlay .stack-file { color: #60a5fa; font-weight: 500; }
#equantic-error-overlay .stack-pos { color: #f472b6; }
#equantic-error-overlay .eq-hl-keyword { color: #c678dd; }
#equantic-error-overlay .eq-hl-func { color: #61afef; }
#equantic-error-overlay .footer {
  padding: 16px 32px; background: rgba(0, 0, 0, 0.2); border-top: 1px solid rgba(255, 255, 255, 0.05);
  font-size: 12px; color: #64748b; display: flex; justify-content: center;
}
`;

export const errorOverlay = new ErrorOverlay();

let installed = false;

/**
 * The browser's notices that ride the error event carrying no exception, and report no fault of the
 * app or the SDK: a ResizeObserver loop that left notifications for the next frame (`ResizeObserver
 * loop completed with undelivered notifications`, once `… limit exceeded`), which a window resize
 * raises whenever an observer's callback changes a size it observes.
 */
function isBrowserNotice(event: ErrorEvent): boolean {
  return event.error == null && /^ResizeObserver loop /.test(event.message ?? '');
}

/**
 * Installs the window-level hooks. EXPLICIT rather than a module side effect: the side-effect
 * version shipped in June and never ran once, because nothing ever imported the module — an
 * installation that depends on being imported is dead code with extra steps. Boot calls this in
 * development; errors still reach the console untouched.
 */
export function installErrorOverlay(): void {
  if (installed || typeof window === 'undefined') return;
  installed = true;

  window.addEventListener('error', (event) => {
    if (isBrowserNotice(event)) return;
    errorOverlay.show({
      kind: 'exception',
      message: event.message,
      stack: (event.error as Error | undefined)?.stack,
    });
  });

  window.addEventListener('unhandledrejection', (event) => {
    const reason: unknown = event.reason;
    errorOverlay.show({
      kind: 'unobserved',
      message: String(reason),
      stack: reason instanceof Error ? reason.stack : undefined,
    });
  });
}
