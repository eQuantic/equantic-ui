import * as vscode from 'vscode';
import { CompileResult, Sidecar } from './sidecar';
import { ProjectLayout } from './project';

/**
 * The preview panel: the real web runtime, rendering the real compiled module, beside the file that
 * produced it.
 *
 * It is deliberately NOT a lookalike. Everything on screen came through the same compiler and the
 * same runtime a served page goes through, so a preview that renders is evidence the page renders —
 * which is the only kind of preview worth having, and the reason this stands on the SDK rather than
 * reimplementing a renderer.
 */
export class PreviewPanel {
  private static readonly viewType = 'equanticUI.preview';

  private readonly panel: vscode.WebviewPanel;
  private readonly disposables: vscode.Disposable[] = [];
  private compileTimer: NodeJS.Timeout | undefined;
  private diagnoseTimer: NodeJS.Timeout | undefined;
  private runtimeUri = '';
  private theme = '';
  /** The last compile that produced a module. A failing edit holds this on screen rather than
   * blanking: an empty frame tells you nothing, and you are usually mid-word when it happens. */
  private lastGood: CompileResult | undefined;
  private generation = 0;

  constructor(
    private readonly document: vscode.TextDocument,
    layout: ProjectLayout,
    private readonly sidecar: Sidecar,
    private readonly diagnostics: vscode.DiagnosticCollection,
    private readonly log: (line: string) => void,
    extensionUri: vscode.Uri,
    private readonly onDisposed: () => void,
  ) {
    this.panel = vscode.window.createWebviewPanel(
      PreviewPanel.viewType,
      `Preview — ${vscode.workspace.asRelativePath(document.uri)}`,
      { viewColumn: vscode.ViewColumn.Beside, preserveFocus: true },
      {
        enableScripts: true,
        // The panel holds a mounted component tree; rebuilding it on every tab switch would throw
        // away scroll position and any state the preview has accumulated.
        retainContextWhenHidden: true,
        localResourceRoots: [extensionUri, vscode.Uri.file(layout.dir)],
      },
    );

    this.runtimeUri = this.panel.webview.asWebviewUri(vscode.Uri.file(layout.runtimePath)).toString();
    this.panel.webview.html = this.shell();

    this.disposables.push(
      this.panel.webview.onDidReceiveMessage((message) => this.onWebviewMessage(message)),
      vscode.workspace.onDidChangeTextDocument((event) => {
        if (event.document.uri.toString() === this.document.uri.toString()) this.schedule();
      }),
      vscode.workspace.onDidCloseTextDocument((closed) => {
        if (closed.uri.toString() === this.document.uri.toString()) this.panel.dispose();
      }),
    );

    this.panel.onDidDispose(() => this.dispose());
    // No first render here: the theme has not arrived yet, and rendering without it would paint an
    // unthemed frame that is replaced a moment later. prime() does both, in order.
  }

  reveal(): void {
    this.panel.reveal(vscode.ViewColumn.Beside, true);
  }

  private config<T>(key: string, fallback: T): T {
    return vscode.workspace.getConfiguration('equanticUI').get<T>(key, fallback);
  }

  /** Two cadences, because the two questions cost different amounts: what is WRONG is a bind
   * (~30 ms) and can be asked while typing continues; what it LOOKS like is a full transpile. */
  private schedule(): void {
    if (this.diagnoseTimer) clearTimeout(this.diagnoseTimer);
    if (this.compileTimer) clearTimeout(this.compileTimer);

    this.diagnoseTimer = setTimeout(() => void this.publishDiagnostics(), this.config('diagnoseDebounceMs', 150));
    this.compileTimer = setTimeout(() => void this.refresh(), this.config('compileDebounceMs', 400));
  }

  private async publishDiagnostics(): Promise<void> {
    try {
      const marks = await this.sidecar.diagnose(this.document.uri.fsPath, this.document.getText());
      this.diagnostics.set(
        this.document.uri,
        marks.map((mark) => {
          const diagnostic = new vscode.Diagnostic(
            new vscode.Range(mark.startLine, mark.startColumn, mark.endLine, mark.endColumn),
            mark.message,
            mark.isError ? vscode.DiagnosticSeverity.Error : vscode.DiagnosticSeverity.Warning,
          );
          diagnostic.code = mark.code;
          diagnostic.source = 'eQuantic UI';
          return diagnostic;
        }),
      );
    } catch (error) {
      this.log(`diagnose failed: ${(error as Error).message}`);
    }
  }

  private async refresh(): Promise<void> {
    const generation = ++this.generation;
    const started = Date.now();

    let result: CompileResult;
    try {
      result = await this.sidecar.compile(this.document.uri.fsPath, this.document.getText());
    } catch (error) {
      this.post({ type: 'error', title: 'The design host could not compile this file', detail: (error as Error).message });
      return;
    }

    // A slower earlier compile must never overwrite a newer one — the preview would show the file
    // as it was two keystrokes ago and stay that way until the next edit.
    if (generation !== this.generation) return;

    if (!result.success) {
      const first = result.marks[0];
      this.post({
        type: 'stale',
        reason: first ? `${first.code}: ${first.message} (line ${first.startLine + 1})` : 'This file does not compile.',
        hasPrevious: this.lastGood !== undefined,
      });
      return;
    }

    this.lastGood = result;
    this.post({ type: 'render', js: result.js, className: result.className, theme: this.theme, runtime: this.runtimeUri });
    this.log(`compiled ${result.className} in ${result.elapsedMs} ms (round trip ${Date.now() - started} ms, ${result.js.length} B)`);
  }

  async prime(): Promise<void> {
    this.theme = await this.sidecar.theme();
    await this.refresh();
  }

  private post(message: unknown): void {
    void this.panel.webview.postMessage(message);
  }

  private onWebviewMessage(message: { type: string; detail?: string }): void {
    if (message.type === 'threw') this.log(`preview threw: ${message.detail ?? ''}`);
  }

  /**
   * The host document. It is written ONCE and then only messaged: replacing the HTML on every
   * keystroke would tear down the runtime and reload it, which is both slower and visibly flickery.
   */
  private shell(): string {
    const nonce = Array.from({ length: 32 }, () => Math.floor(Math.random() * 36).toString(36)).join('');
    const csp = [
      "default-src 'none'",
      // blob: is the compiled module — it arrives by message and is imported as a module URL, which
      // is the only way to re-evaluate an ES module without reloading the document.
      `script-src 'nonce-${nonce}' ${this.panel.webview.cspSource} blob:`,
      `style-src ${this.panel.webview.cspSource} 'unsafe-inline'`,
      `font-src ${this.panel.webview.cspSource} data:`,
      `img-src ${this.panel.webview.cspSource} data: blob:`,
    ].join('; ');

    return /* html */ `<!doctype html>
<html>
<head>
<meta charset="utf-8">
<meta http-equiv="Content-Security-Policy" content="${csp}">
<style>
  html, body { margin: 0; padding: 0; height: 100%; }
  #app { height: 100%; box-sizing: border-box; }
  #notice {
    position: fixed; left: 0; right: 0; bottom: 0; margin: 0; padding: 8px 12px;
    font: 12px/1.5 var(--vscode-editor-font-family, ui-monospace, monospace);
    background: var(--vscode-inputValidation-errorBackground, #5a1d1d);
    color: var(--vscode-inputValidation-errorForeground, #fff);
    white-space: pre-wrap; display: none;
  }
  #notice.visible { display: block; }
  .eq-preview-error {
    margin: 0; padding: 20px; white-space: pre-wrap;
    font: 13px/1.7 var(--vscode-editor-font-family, ui-monospace, monospace);
    color: var(--vscode-errorForeground, #e5645c);
  }
</style>
</head>
<body>
<div id="app"></div>
<pre id="notice"></pre>
<script nonce="${nonce}">
(function () {
  const vscode = acquireVsCodeApi();
  const app = document.getElementById('app');
  const notice = document.getElementById('notice');
  let objectUrl;

  function report(title, detail) {
    const box = document.createElement('pre');
    box.className = 'eq-preview-error';
    box.textContent = title + '\\n\\n' + detail;
    app.replaceChildren(box);
    vscode.postMessage({ type: 'threw', detail: title + ' ' + detail });
  }

  // Installed BEFORE any module runs, and outside it. A module with a syntax error never executes,
  // so a try/catch written inside it does not exist to catch anything — which is exactly how a
  // preview goes blank with nothing anywhere to say why.
  window.addEventListener('error', (e) => report('This module did not run:', (e.error && e.error.stack) || e.message));
  window.addEventListener('unhandledrejection', (e) =>
    report('Something failed after it started:', (e.reason && e.reason.stack) || String(e.reason)));

  async function render(payload) {
    notice.classList.remove('visible');

    // The bare specifier has no import map behind it here, so it is pointed straight at the
    // runtime the project's own build produced.
    const source = payload.js.replace(/"@equantic\\/runtime"/g, JSON.stringify(payload.runtime));
    const module = new Blob([source], { type: 'text/javascript' });

    // The previous module's URL is revoked only after the next one is in hand, so a failed import
    // leaves the running preview alone.
    const nextUrl = URL.createObjectURL(module);
    try {
      const loaded = await import(nextUrl);
      const runtime = await import(payload.runtime);
      if (payload.theme) {
        runtime.detectPhotonDensity();
        runtime.setPhotonTheme(runtime.materializeTheme(JSON.parse(payload.theme)));
      }
      const Component = loaded[payload.className];
      if (typeof Component !== 'function') {
        report('Nothing to mount:', 'the compiled module has no export named ' + payload.className);
        return;
      }
      app.replaceChildren();
      new Component().mount(app);
      if (objectUrl) URL.revokeObjectURL(objectUrl);
      objectUrl = nextUrl;
    } catch (error) {
      URL.revokeObjectURL(nextUrl);
      report('This ran, and then threw:', (error && error.stack) || String(error));
    }
  }

  window.addEventListener('message', (event) => {
    const payload = event.data;
    if (payload.type === 'render') void render(payload);
    else if (payload.type === 'stale') {
      // The last good render stays on screen. A blank frame while you are mid-word is worse than a
      // slightly old one with a line telling you what is wrong.
      notice.textContent = payload.hasPrevious
        ? payload.reason + '\\n(showing the last version that compiled)'
        : payload.reason;
      notice.classList.add('visible');
    } else if (payload.type === 'error') report(payload.title, payload.detail);
  });
}());
</script>
</body>
</html>`;
  }

  private disposed = false;

  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    if (this.compileTimer) clearTimeout(this.compileTimer);
    if (this.diagnoseTimer) clearTimeout(this.diagnoseTimer);
    this.diagnostics.delete(this.document.uri);
    for (const disposable of this.disposables) disposable.dispose();
    this.disposables.length = 0;
    this.onDisposed();
  }
}
