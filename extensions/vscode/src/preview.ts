import * as vscode from 'vscode';
import { CompileResult, OpenBuffer, Sidecar } from './sidecar';
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
  /** Resolves when the webview's script has registered its message listener. */
  private ready!: Promise<void>;
  private announceReady!: () => void;
  private theme = '';
  /** The last compile that produced a module. A failing edit holds this on screen rather than
   * blanking: an empty frame tells you nothing, and you are usually mid-word when it happens. */
  private lastGood: CompileResult | undefined;
  private generation = 0;

  constructor(
    private readonly document: vscode.TextDocument,
    private readonly layout: ProjectLayout,
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

    this.ready = new Promise<void>((resolve) => { this.announceReady = resolve; });

    this.runtimeUri = this.panel.webview.asWebviewUri(vscode.Uri.file(layout.runtimePath)).toString();
    this.panel.webview.html = this.shell();

    this.disposables.push(
      this.panel.webview.onDidReceiveMessage((message) => this.onWebviewMessage(message)),
      // ANY C# file in the project, not only the previewed one: a screen is composed of several,
      // and editing the shell beside it has to repaint just the same.
      vscode.workspace.onDidChangeTextDocument((event) => {
        if (event.document.languageId === 'csharp' && event.document.uri.fsPath.startsWith(this.layout.dir)) {
          this.schedule();
        }
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
      const marks = await this.sidecar.diagnose(this.document.uri.fsPath, this.document.getText(), this.buffers());
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
      result = await this.sidecar.compile(this.document.uri.fsPath, this.document.getText(), this.buffers());
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
    // The webview has to be listening before the first render is posted, or it is dropped and the
    // panel sits empty until the next keystroke.
    await this.ready;
    await this.refresh();
  }

  /**
   * Every C# file the editor holds UNSAVED inside this project.
   * <para>
   * A screen is rarely one file — a shell, a row, a data helper — so a preview built from the edited
   * buffer and everything else from disk would show the last saved version of all the parts the
   * author is not currently typing in. Scoped to the project, because the host's compilation is.
   * </para>
   */
  private buffers(): OpenBuffer[] {
    return vscode.workspace.textDocuments
      .filter((document) =>
        document.isDirty
        && document.languageId === 'csharp'
        && document.uri.scheme === 'file'
        && document.uri.fsPath.startsWith(this.layout.dir))
      .map((document) => ({ path: document.uri.fsPath, text: document.getText() }));
  }

  private post(message: unknown): void {
    void this.panel.webview.postMessage(message);
  }

  private onWebviewMessage(
    message: { type: string; detail?: string; origin?: string; property?: string; value?: string; options?: string[] },
  ): void {
    if (message.type === 'ready') this.announceReady();
    else if (message.type === 'threw') this.log(`preview threw: ${message.detail ?? ''}`);
    else if (message.type === 'select' && message.origin) void this.revealSource(message.origin);
    else if (message.type === 'edit' && message.origin && message.property) {
      void this.editProperty(message.origin, message.property, message.value, message.options);
    }
  }

  /**
   * Ask for the new value and write it.
   * <para>
   * Asked through VS Code's OWN input rather than a field in the webview: an enum becomes a quick
   * pick with the members on it, everything else an input box seeded with what is written now. It is
   * keyboard-navigable and screen-reader-correct for free, which a hand-rolled panel field is not.
   * </para>
   */
  private async editProperty(
    origin: string, property: string, current: string | undefined, options: string[] | undefined,
  ): Promise<void> {
    const value = options?.length
      // The members are offered as they must be WRITTEN, qualified — the panel's job is to spare
      // the author from knowing the type's name, not to produce something that does not compile.
      ? await vscode.window.showQuickPick(options, { title: `${property}`, placeHolder: current ?? 'choose a value' })
      : await vscode.window.showInputBox({
          title: property,
          value: current ?? '',
          prompt: 'C# expression — this goes into the file as written',
        });

    if (value === undefined) return;

    const span = PreviewPanel.parseOrigin(origin);
    if (!span) return;

    const edit = await this.sidecar.setProperty(
      span.path, await this.textOf(span.path), origin, property, value, this.buffers());

    if (!edit.applied) {
      void vscode.window.showWarningMessage(`eQuantic UI: ${edit.reason ?? 'that edit was refused'}`);
      return;
    }

    // ONE WorkspaceEdit for the gesture, applied to the document rather than written to disk: it
    // joins the editor's undo stack, so a single Ctrl+Z takes it back, and an unsaved buffer stays
    // unsaved. The preview recompiles from the buffer on its usual debounce.
    const workspaceEdit = new vscode.WorkspaceEdit();
    workspaceEdit.replace(
      vscode.Uri.file(span.path),
      new vscode.Range(edit.startLine, edit.startColumn, edit.endLine, edit.endColumn),
      edit.newText);

    if (!(await vscode.workspace.applyEdit(workspaceEdit))) {
      void vscode.window.showWarningMessage('eQuantic UI: the editor refused that edit.');
    }
  }

  /**
   * A pixel, answered with the C# that built it: open the file the origin names and put the
   * selection on the exact expression.
   *
   * The file is often NOT the one being previewed — a page composes components that live beside it,
   * and their nodes carry their own file's spans. That is a feature of stamping at the construction:
   * you land where the thing is actually written.
   */
  private async revealSource(origin: string): Promise<void> {
    const span = PreviewPanel.parseOrigin(origin);
    if (!span) {
      this.log(`ignored an unparseable origin: ${origin}`);
      return;
    }

    try {
      const document = await vscode.workspace.openTextDocument(vscode.Uri.file(span.path));
      const editor = await vscode.window.showTextDocument(document, {
        // Beside the preview, not on top of it — the whole point is seeing both.
        viewColumn: vscode.ViewColumn.One,
        preserveFocus: false,
        preview: false,
      });
      editor.selection = new vscode.Selection(span.start, span.end);
      editor.revealRange(new vscode.Range(span.start, span.end), vscode.TextEditorRevealType.InCenterIfOutsideViewport);
    } catch (error) {
      this.log(`could not reveal ${span.path}: ${(error as Error).message}`);
    }

    // Then say what KIND of node it is. Sent back separately rather than asked on hover: this is a
    // round trip, and one per pointer move would be a request storm for an answer nobody read.
    // The ORIGIN decides the file, not the panel. A screen is composed of several — a shell, a row,
    // a helper — and their nodes carry their own file's spans; asking about the previewed file would
    // answer "not mine" for most of what is on screen.
    const path = span.path;
    const text = await this.textOf(path);

    try {
      const buffers = this.buffers();
      const [tier, inspected] = await Promise.all([
        this.sidecar.classify(path, text, origin, buffers),
        this.sidecar.inspect(path, text, origin, buffers),
      ]);
      this.post({
        type: 'selected',
        tier: tier.tier,
        reason: tier.reason,
        node: inspected === '' ? undefined : inspected,
      });
    } catch (error) {
      this.log(`inspect failed: ${(error as Error).message}`);
    }
  }

  /** A file as the editor has it — its unsaved buffer when one is open, and its saved text
   * otherwise. openTextDocument answers for both, and returns the already-open document when there
   * is one rather than a second copy of it. */
  private async textOf(path: string): Promise<string> {
    const open = vscode.workspace.textDocuments.find((document) => document.uri.fsPath === path);
    if (open) return open.getText();
    return (await vscode.workspace.openTextDocument(vscode.Uri.file(path))).getText();
  }

  /** `path|startLine:startColumn|endLine:endColumn`, zero-based — the editor's own coordinates. */
  private static parseOrigin(origin: string): { path: string; start: vscode.Position; end: vscode.Position } | undefined {
    const parts = origin.split('|');
    if (parts.length !== 3) return undefined;

    const position = (value: string): vscode.Position | undefined => {
      const [line, column] = value.split(':').map(Number);
      return Number.isFinite(line) && Number.isFinite(column) ? new vscode.Position(line, column) : undefined;
    };

    const start = position(parts[1]);
    const end = position(parts[2]);
    return start && end ? { path: parts[0], start, end } : undefined;
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
  html { color-scheme: light dark; }
  /* Painted from the THEME, not left transparent: an unpainted preview shows the editor's colours
     through whatever the app has not drawn, which reads as a broken screen under any theme that is
     not the one the app expects. Replaced by the real background as soon as the theme arrives. */
  html, body { margin: 0; padding: 0; height: 100%; background: var(--eq-preview-bg, Canvas); }
  #app { height: 100%; box-sizing: border-box; }
  #notice {
    position: fixed; left: 0; right: 0; bottom: 0; margin: 0; padding: 8px 12px;
    font: 12px/1.5 var(--vscode-editor-font-family, ui-monospace, monospace);
    background: var(--vscode-inputValidation-errorBackground, #5a1d1d);
    color: var(--vscode-inputValidation-errorForeground, #fff);
    white-space: pre-wrap; display: none;
  }
  #notice.visible { display: block; }
  /* The selection chrome lives OUTSIDE #app so it survives a remount, and is pointer-transparent so
     it never becomes the thing a click lands on. */
  #outline {
    position: fixed; pointer-events: none; display: none; z-index: 2;
    border: 1px solid var(--vscode-focusBorder, #0078d4);
    background: color-mix(in srgb, var(--vscode-focusBorder, #0078d4) 12%, transparent);
  }
  #outline.visible { display: block; }
  /* The tier is drawn, not only written: a solid frame is a node you may edit where it stands, a
     dashed one comes from a loop or a branch, a dotted one is written in another member entirely. */
  #outline.derived { border-style: dashed; }
  /* The name rides WITH the frame, and is pointer-transparent like it — a label that could be
     hovered would steal the pointer from the thing it is naming. */
  #badge {
    position: fixed; pointer-events: none; display: none; z-index: 3;
    font: 10px/1.4 var(--vscode-font-family, sans-serif);
    padding: 2px 6px; border-radius: 3px; white-space: nowrap;
    background: var(--vscode-focusBorder, #0078d4);
    color: var(--vscode-button-foreground, #fff);
  }
  #badge.visible { display: block; }
  #outline.foreign { border-style: dotted; opacity: 0.75; }
  #tier {
    position: fixed; left: 8px; bottom: 8px; right: 8px; z-index: 3; display: none;
    max-height: 45%; overflow: auto;
    font: 11px/1.5 var(--vscode-font-family, sans-serif);
    padding: 6px 9px; border-radius: 4px;
    background: var(--vscode-editorWidget-background, #252526);
    color: var(--vscode-editorWidget-foreground, #ccc);
    border: 1px solid var(--vscode-editorWidget-border, #454545);
  }
  #tier.visible { display: block; }
  #tier b { color: var(--vscode-textLink-foreground, #4daafc); }
  #tier table { border-collapse: collapse; width: 100%; margin-top: 6px; }
  #tier td { padding: 2px 6px 2px 0; vertical-align: top; }
  #tier td.name { white-space: nowrap; }
  #tier td.type { opacity: 0.6; white-space: nowrap; }
  #tier td.value {
    font-family: var(--vscode-editor-font-family, ui-monospace, monospace);
    word-break: break-all;
  }
  #tier tr.settable { cursor: pointer; }
  #tier tr.settable:hover { background: var(--vscode-list-hoverBackground, #2a2d2e); }
  #tier tr.unreachable { opacity: 0.5; }
  #tier tr.unreachable td.value::after {
    content: attr(data-reason); opacity: 0.9; font-family: var(--vscode-font-family, sans-serif);
  }
  #inspect {
    position: fixed; top: 8px; right: 8px; z-index: 3;
    font: 11px/1 var(--vscode-font-family, sans-serif);
    padding: 5px 9px; border-radius: 4px; cursor: pointer;
    border: 1px solid var(--vscode-button-border, transparent);
    background: var(--vscode-button-secondaryBackground, #3a3d41);
    color: var(--vscode-button-secondaryForeground, #fff);
  }
  #inspect[aria-pressed="true"] {
    background: var(--vscode-button-background, #0078d4);
    color: var(--vscode-button-foreground, #fff);
  }
  .eq-preview-error {
    margin: 0; padding: 20px; white-space: pre-wrap;
    font: 13px/1.7 var(--vscode-editor-font-family, ui-monospace, monospace);
    color: var(--vscode-errorForeground, #e5645c);
  }
</style>
</head>
<body>
<div id="app"></div>
<div id="outline"></div>
<div id="badge"></div>
<button id="inspect" type="button" aria-pressed="false" title="Click an element to reveal the C# that built it">Inspect</button>
<div id="tier"></div>
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

  // The app's own background token. The mode is chosen HERE rather than left to the CSS light-dark()
  // function, which is recent enough that a webview's Chromium may not have it — and a background
  // that silently does not apply is the exact problem this is fixing. Re-applied when the viewer's
  // scheme changes, so the preview follows it without a reload.
  let themeSurfaces = null;

  function paintBackground(theme) {
    if (theme && theme.surfaces && theme.surfaces.background) themeSurfaces = theme.surfaces.background;
    if (!themeSurfaces || themeSurfaces.length < 2) return;
    const dark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    const c = themeSurfaces[dark ? 1 : 0];
    document.documentElement.style.setProperty('--eq-preview-bg', 'rgb(' + c[0] + ' ' + c[1] + ' ' + c[2] + ')');
  }

  window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => paintBackground(null));

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
        const theme = JSON.parse(payload.theme);
        runtime.detectPhotonDensity();
        runtime.setPhotonTheme(runtime.materializeTheme(theme));
        paintBackground(theme);
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

  // ---- inspect: a click on a pixel, answered with the C# expression that built it ----------------
  //
  // A MODE, not a modifier. The preview is a running app: its buttons do things, and a click that
  // both pressed a button and moved the editor's cursor would be two gestures wearing one costume.
  // With inspect on, the app stops receiving pointer events entirely — capture-phase and
  // preventDefault, so nothing downstream sees the click either.
  const inspect = document.getElementById('inspect');
  const outline = document.getElementById('outline');
  const badge = document.getElementById('badge');
  let inspecting = false;

  function setInspecting(on) {
    inspecting = on;
    inspect.setAttribute('aria-pressed', String(on));
    if (!on) { outline.classList.remove('visible'); badge.classList.remove('visible'); }
  }

  inspect.addEventListener('click', (e) => {
    e.stopPropagation();
    setInspecting(!inspecting);
  });

  // The nearest ancestor that KNOWS where it came from. A component is several elements deep — a
  // Button is button > div > div > span — so the clicked node is usually not the stamped one, and
  // treating DOM and node as 1:1 selects nothing most of the time.
  function stamped(target) {
    let node = target;
    while (node && node !== document.body) {
      if (node.getAttribute && node.getAttribute('data-eq-origin')) return node;
      node = node.parentElement;
    }
    return null;
  }

  function frame(element) {
    const box = element.getBoundingClientRect();
    outline.style.left = box.left + 'px';
    outline.style.top = box.top + 'px';
    outline.style.width = box.width + 'px';
    outline.style.height = box.height + 'px';
    outline.classList.add('visible');

    const name = element.getAttribute('data-eq-component');
    if (!name) { badge.classList.remove('visible'); return; }
    badge.textContent = name;
    badge.classList.add('visible');
    // Above the frame when there is room for it, tucked inside the top edge when there is not —
    // a label that hangs off the top of the panel names nothing.
    const height = badge.offsetHeight || 16;
    badge.style.left = Math.max(2, box.left) + 'px';
    badge.style.top = (box.top >= height + 2 ? box.top - height - 2 : box.top + 2) + 'px';
  }

  // Focus is what actually has to be stopped, not the click. The framework's forms are quiet until
  // TOUCHED, and a field is touched when it loses focus — so clicking one input and then another ran
  // validation across the form while selecting nothing. preventDefault on pointerdown/mousedown stops
  // focus from ever moving; the click still fires afterwards, which is what selection listens to.
  for (const name of ['pointerdown', 'mousedown']) {
    document.addEventListener(name, (e) => {
      if (!inspecting || e.target === inspect) return;
      e.preventDefault();
      e.stopPropagation();
    }, true);
  }

  document.addEventListener('pointermove', (e) => {
    if (!inspecting) return;
    const found = stamped(e.target);
    if (found) frame(found);
    else { outline.classList.remove('visible'); badge.classList.remove('visible'); }
  }, true);

  const tier = document.getElementById('tier');
  let selectedOrigin = null;

  // Built with DOM calls rather than an HTML string: every value here is SOURCE TEXT the developer
  // wrote, and interpolating that into innerHTML would let a string literal in their own file close
  // a tag. textContent cannot.
  function showSelection(payload) {
    tier.replaceChildren();

    const heading = document.createElement('div');
    const kind = document.createElement('b');
    kind.textContent = payload.node ? payload.node.component : payload.tier;
    heading.appendChild(kind);
    heading.appendChild(document.createTextNode(
      (payload.node ? ' · ' + payload.node.form + ' · ' + payload.tier : '') + ' — ' + payload.reason));
    tier.appendChild(heading);

    if (payload.node && payload.node.summary) {
      const summary = document.createElement('div');
      summary.style.opacity = '0.75';
      summary.style.marginTop = '4px';
      summary.textContent = payload.node.summary;
      tier.appendChild(summary);
    }

    if (payload.node) {
      const table = document.createElement('table');
      for (const property of payload.node.properties) {
        // An unset property nobody can reach from this form is noise; an unset one they COULD set is
        // the offer the panel exists to make.
        if (property.kind === 'unset' && !property.editable && !property.reason) continue;

        const row = document.createElement('tr');
        if (!property.editable) row.className = 'unreachable';
        else {
          row.className = 'settable';
          row.title = 'Set ' + property.name;
          row.addEventListener('click', () => vscode.postMessage({
            type: 'edit',
            origin: selectedOrigin,
            property: property.name,
            value: property.value,
            options: property.options,
          }));
        }

        const name = document.createElement('td');
        name.className = 'name';
        name.textContent = property.name;
        if (property.summary) name.title = property.summary;

        const type = document.createElement('td');
        type.className = 'type';
        type.textContent = property.type;

        const value = document.createElement('td');
        value.className = 'value';
        if (property.value) value.textContent = property.value.replace(/\\s+/g, ' ').slice(0, 90);
        else if (property.editable) value.textContent = '—';
        else value.setAttribute('data-reason', property.reason || 'not reachable from this form');

        row.append(name, type, value);
        table.appendChild(row);
      }
      tier.appendChild(table);
    }

    tier.classList.add('visible');
  }

  document.addEventListener('click', (e) => {
    if (!inspecting || e.target === inspect) return;
    e.preventDefault();
    e.stopPropagation();
    const found = stamped(e.target);
    if (!found) return;
    // The frame is drawn NOW and re-dressed when the tier comes back, so the click feels answered
    // before the round trip finishes.
    frame(found);
    outline.classList.remove('derived', 'foreign');
    tier.classList.remove('visible');
    selectedOrigin = found.getAttribute('data-eq-origin');
    vscode.postMessage({ type: 'select', origin: selectedOrigin });
  }, true);

  // Announced only after the listener above exists. A message posted before this point is simply
  // dropped by the webview, which is how a first render can go missing and the panel sit empty until
  // the next keystroke.
  vscode.postMessage({ type: 'ready' });

  window.addEventListener('message', (event) => {
    const payload = event.data;
    // The outline framed an element of the PREVIOUS tree; after a recompile that rectangle means
    // nothing. The next pointer move draws a true one.
    if (payload.type === 'render') {
      outline.classList.remove('visible');
      badge.classList.remove('visible');
      tier.classList.remove('visible');
      void render(payload);
    } else if (payload.type === 'selected') {
      if (payload.tier !== 'literal') outline.classList.add(payload.tier);
      showSelection(payload);
    }
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
