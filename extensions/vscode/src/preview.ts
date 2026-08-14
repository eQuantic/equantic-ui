import * as vscode from 'vscode';
import { CompileResult, OpenBuffer, PaletteEntry, Sidecar } from './sidecar';
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
    private readonly onFocused: (focused: boolean) => void,
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

    // Everything past createWebviewPanel is wiring, and anything that throws in it would leave the
    // panel ON SCREEN with nothing in it — a black rectangle beside a toast. A failed open should
    // leave no trace.
    try {
      this.wire(layout);
    } catch (error) {
      this.panel.dispose();
      throw error;
    }
  }

  private wire(layout: ProjectLayout): void {
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

    this.disposables.push(this.panel.onDidChangeViewState((event) => {
      this.onFocused(event.webviewPanel.active);
      // The title bar shows one of two buttons by this key, so it has to say what THIS panel is
      // doing the moment it becomes the active one.
      if (event.webviewPanel.active) void this.publishInspectContext();
    }));
    // NOT announced here: the callback closes over the `const panel` this constructor is still
    // running inside, and touching it before the assignment completes is a temporal-dead-zone
    // error. The caller announces it once construction has returned.
    this.panel.onDidDispose(() => this.dispose());
    // No first render here: the theme has not arrived yet, and rendering without it would paint an
    // unthemed frame that is replaced a moment later. prime() does both, in order.
  }

  reveal(): void {
    this.panel.reveal(vscode.ViewColumn.Beside, true);
  }

  private inspecting = false;

  /**
   * Inspect mode, driven from the panel's own title bar.
   * <para>
   * The buttons live in VS Code's title bar rather than in the document, so they ARE the editor's
   * UI — its theme, its icons, its hover behaviour — instead of a drawing of it. Which of the two
   * (start/stop) is shown is decided by a context key, the idiomatic way to spell a toggle.
   * </para>
   */
  setInspecting(on: boolean): void {
    this.inspecting = on;
    this.post({ type: 'inspect', on });
    void this.publishInspectContext();
  }

  private publishInspectContext(): Promise<unknown> {
    return Promise.resolve(
      vscode.commands.executeCommand('setContext', 'equanticUI.inspecting', this.inspecting));
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
    message: {
      type: string; detail?: string; origin?: string; property?: string; value?: string;
      options?: string[]; index?: number;
    },
  ): void {
    if (message.type === 'ready') this.announceReady();
    else if (message.type === 'threw') this.log(`preview threw: ${message.detail ?? ''}`);
    else if (message.type === 'select' && message.origin) void this.revealSource(message.origin);
    else if (message.type === 'edit' && message.origin && message.property) {
      void this.editProperty(message.origin, message.property, message.value, message.options);
    } else if (message.type === 'insert' && message.origin && typeof message.index === 'number') {
      void this.insertChild(message.origin, message.index);
    }
  }

  /** Cached for the session: the surface does not change while the editor is open, and a quick pick
   * that waits on a round trip feels like a stall. */
  private paletteEntries: PaletteEntry[] | undefined;

  /**
   * Insert a component into a declarative children list.
   * <para>
   * Offered only where the host has already said there IS such a list — an affordance that appears
   * and then refuses reads as a bug, so the panel asks before it draws the control rather than after
   * it is pressed.
   * </para>
   */
  private async insertChild(origin: string, index: number): Promise<void> {
    const span = PreviewPanel.parseOrigin(origin);
    if (!span) return;

    try {
      this.paletteEntries ??= await this.sidecar.palette();

      const pick = await vscode.window.showQuickPick(
        this.paletteEntries.map((entry) => ({
          label: entry.name,
          description: entry.snippet,
          detail: entry.summary ?? undefined,
          entry,
        })),
        { title: 'Insert a component', matchOnDescription: true, matchOnDetail: true },
      );
      if (!pick) return;

      const edit = await this.sidecar.insertChild(
        span.path, await this.textOf(span.path), origin, index, pick.entry.snippet, this.buffers());

      if (!edit.applied) {
        void vscode.window.showWarningMessage(`eQuantic UI: ${edit.reason ?? 'that insertion was refused'}`);
        return;
      }

      const workspaceEdit = new vscode.WorkspaceEdit();
      workspaceEdit.replace(
        vscode.Uri.file(span.path),
        new vscode.Range(edit.startLine, edit.startColumn, edit.endLine, edit.endColumn),
        edit.newText);

      if (await vscode.workspace.applyEdit(workspaceEdit)) await this.describe(origin);
    } catch (error) {
      this.log(`insert failed: ${(error as Error).message}`);
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
      return;
    }

    // The panel is still showing the values from BEFORE the edit — including the one just changed.
    // Re-asked rather than patched locally, because an edit can move more than the field it touched:
    // adding a named argument changes what "unset" means for the rest of the row list.
    await this.describe(origin);
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

    await this.describe(origin);
  }

  /**
   * What the panel shows for an origin: its tier, and what it holds.
   * <para>
   * Asked on selection and again after every edit, never on hover — it is a round trip, and one per
   * pointer move would be a request storm for an answer nobody reads. Hover already has the name,
   * carried in the DOM.
   * </para>
   * <para>
   * The ORIGIN decides the file, not the panel. A screen is composed of several — a shell, a row, a
   * helper — and their nodes carry their own file's spans, so asking about the previewed file would
   * answer "not mine" for most of what is on screen.
   * </para>
   */
  private async describe(origin: string): Promise<void> {
    const span = PreviewPanel.parseOrigin(origin);
    if (!span) return;

    try {
      const text = await this.textOf(span.path);
      const buffers = this.buffers();
      const [tier, inspected] = await Promise.all([
        this.sidecar.classify(span.path, text, origin, buffers),
        this.sidecar.inspect(span.path, text, origin, buffers),
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
     not the one the app expects. */
  html, body { margin: 0; padding: 0; height: 100%; background: var(--eq-preview-bg, Canvas); }
  #app { height: 100%; box-sizing: border-box; }

  /* The selection chrome lives OUTSIDE #app so it survives a remount, and is pointer-transparent so
     it never becomes the thing a click lands on. */
  #outline {
    position: fixed; pointer-events: none; display: none; z-index: 2;
    border: 1px solid var(--vscode-focusBorder, #0078d4);
    background: color-mix(in srgb, var(--vscode-focusBorder, #0078d4) 12%, transparent);
  }
  #outline.visible { display: block; }
  /* A frame is drawn for what may be edited where it stands, dashed for what comes from a loop or a
     branch, dotted for what is written somewhere else. */
  #outline.derived { border-style: dashed; }
  #outline.foreign { border-style: dotted; opacity: 0.75; }

  #badge {
    position: fixed; pointer-events: none; display: none; z-index: 3;
    font: 10px/1.4 var(--vscode-font-family, sans-serif);
    padding: 2px 6px; border-radius: 3px; white-space: nowrap;
    background: var(--vscode-focusBorder, #0078d4);
    color: var(--vscode-button-foreground, #fff);
  }
  #badge.visible { display: block; }

  /* ---- the properties panel -------------------------------------------------------------------
     Docked to the bottom and OVERLAYING the app rather than pushing it: a panel that took height
     would re-lay-out the screen being previewed, so what you designed against would not be what you
     shipped. Built from VS Code's own variables, so it follows the editor's theme without knowing
     which one is on. */
  #panel {
    position: fixed; left: 0; right: 0; bottom: 0; z-index: 4;
    display: none; flex-direction: column; max-height: 55%;
    background: var(--vscode-panel-background, var(--vscode-editor-background, #1e1e1e));
    border-top: 1px solid var(--vscode-panel-border, rgba(128,128,128,0.35));
    color: var(--vscode-foreground, #ccc);
    font: 12px/1.5 var(--vscode-font-family, sans-serif);
  }
  #panel.visible { display: flex; }
  #panel.minimised #panel-body { display: none; }

  #panel-head {
    display: flex; align-items: center; gap: 8px; padding: 0 4px 0 10px;
    height: 28px; flex: none;
    border-bottom: 1px solid var(--vscode-panel-border, rgba(128,128,128,0.35));
  }
  #panel-title {
    font-weight: 600; text-transform: none; white-space: nowrap;
    color: var(--vscode-panelTitle-activeForeground, var(--vscode-foreground, #e7e7e7));
  }
  #panel-tier {
    font-size: 10px; padding: 1px 6px; border-radius: 8px; white-space: nowrap;
    border: 1px solid var(--vscode-panel-border, rgba(128,128,128,0.4));
    color: var(--vscode-descriptionForeground, #9d9d9d);
  }
  #panel-spacer { flex: 1 1 auto; }
  #panel button.icon {
    display: flex; align-items: center; justify-content: center;
    width: 22px; height: 22px; padding: 0; border: none; border-radius: 5px;
    background: transparent; color: var(--vscode-icon-foreground, #c5c5c5); cursor: pointer;
  }
  #panel button.icon:hover { background: var(--vscode-toolbar-hoverBackground, rgba(90,93,94,0.31)); }
  #panel button.icon:focus-visible { outline: 1px solid var(--vscode-focusBorder, #0078d4); }

  #panel-body { overflow: auto; padding: 6px 10px 10px; }
  /* Moving through the tree, from the tree itself: the panel names the parent and the children it
     can see, and selecting one is the same gesture as clicking it on the canvas. */
  #panel-tree { display: flex; flex-wrap: wrap; gap: 4px; align-items: center; padding: 0 0 8px; }
  #panel-tree .chip {
    font: 11px/1.4 var(--vscode-font-family, sans-serif);
    padding: 2px 8px; border-radius: 10px; cursor: pointer;
    border: 1px solid var(--vscode-panel-border, rgba(128,128,128,0.4));
    background: transparent; color: var(--vscode-foreground, #ccc);
  }
  #panel-tree .chip:hover { background: var(--vscode-toolbar-hoverBackground, rgba(90,93,94,0.31)); }
  #panel-tree .chip.add {
    border-style: dashed;
    color: var(--vscode-textLink-foreground, #4daafc);
  }
  #panel-tree .label {
    font: 11px/1.4 var(--vscode-font-family, sans-serif);
    color: var(--vscode-descriptionForeground, #9d9d9d);
  }
  #panel-note {
    color: var(--vscode-descriptionForeground, #9d9d9d);
    padding: 2px 0 6px;
  }
  table.props { border-collapse: collapse; width: 100%; }
  table.props td { padding: 2px 8px 2px 0; vertical-align: middle; }
  td.pname { white-space: nowrap; width: 1%; }
  td.ptype {
    white-space: nowrap; width: 1%;
    color: var(--vscode-descriptionForeground, #9d9d9d);
  }
  tr.unreachable td { color: var(--vscode-disabledForeground, #888); }
  tr.unreachable td.pvalue { font-style: italic; }

  /* The editors, dressed as the editor's own. */
  .props input[type="text"], .props input[type="number"], .props select {
    width: 100%; box-sizing: border-box; padding: 2px 6px; border-radius: 2px;
    font: inherit;
    color: var(--vscode-input-foreground, #ccc);
    background: var(--vscode-input-background, #313131);
    border: 1px solid var(--vscode-input-border, transparent);
  }
  .props select {
    color: var(--vscode-dropdown-foreground, #ccc);
    background: var(--vscode-dropdown-background, #313131);
    border-color: var(--vscode-dropdown-border, transparent);
  }
  .props input:focus, .props select:focus {
    outline: none; border-color: var(--vscode-focusBorder, #0078d4);
  }
  .props input[type="checkbox"] { accent-color: var(--vscode-focusBorder, #0078d4); }

  #notice {
    position: fixed; left: 0; right: 0; bottom: 0; margin: 0; padding: 8px 12px; z-index: 5;
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
<div id="outline"></div>
<div id="badge"></div>

<div id="panel">
  <div id="panel-head">
    <span id="panel-title"></span>
    <span id="panel-tier"></span>
    <span id="panel-spacer"></span>
    <button class="icon" id="panel-min" type="button" title="Minimize" aria-label="Minimize">
      <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
        <path d="M4 6.5l4 4 4-4" stroke="currentColor" stroke-width="1.2" stroke-linecap="round" stroke-linejoin="round"/>
      </svg>
    </button>
    <button class="icon" id="panel-close" type="button" title="Close" aria-label="Close">
      <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
        <path d="M4 4l8 8M12 4l-8 8" stroke="currentColor" stroke-width="1.2" stroke-linecap="round"/>
      </svg>
    </button>
  </div>
  <div id="panel-body"></div>
</div>

<pre id="notice"></pre>
<script nonce="${nonce}">
(function () {
  const vscode = acquireVsCodeApi();
  const app = document.getElementById('app');
  const notice = document.getElementById('notice');
  const outline = document.getElementById('outline');
  const badge = document.getElementById('badge');
  const panel = document.getElementById('panel');
  const panelTitle = document.getElementById('panel-title');
  const panelTier = document.getElementById('panel-tier');
  const panelBody = document.getElementById('panel-body');
  let objectUrl;
  let selectedOrigin = null;
  let selectedElement = null;

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
  // that silently does not apply is the exact problem this is fixing.
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
  // A MODE, driven from the panel's title bar. The preview is a running app: its buttons do things,
  // and a click that both pressed a button and moved the editor's cursor would be two gestures
  // wearing one costume.
  let inspecting = false;

  function setInspecting(on) {
    inspecting = on;
    if (!on) { outline.classList.remove('visible'); badge.classList.remove('visible'); }
  }

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
    // Above the frame when there is room for it, tucked inside the top edge when there is not.
    const height = badge.offsetHeight || 16;
    badge.style.left = Math.max(2, box.left) + 'px';
    badge.style.top = (box.top >= height + 2 ? box.top - height - 2 : box.top + 2) + 'px';
  }

  function insideChrome(target) {
    return panel.contains(target);
  }

  // Focus is what actually has to be stopped, not the click. The framework's forms are quiet until
  // TOUCHED, and a field is touched when it loses focus — so clicking one input and then another ran
  // validation across the form while selecting nothing. preventDefault on pointerdown/mousedown stops
  // focus from ever moving; the click still fires afterwards, which is what selection listens to.
  for (const name of ['pointerdown', 'mousedown']) {
    document.addEventListener(name, (e) => {
      if (!inspecting || insideChrome(e.target)) return;
      e.preventDefault();
      e.stopPropagation();
    }, true);
  }

  document.addEventListener('pointermove', (e) => {
    if (!inspecting || insideChrome(e.target)) return;
    const found = stamped(e.target);
    if (found) frame(found);
    else { outline.classList.remove('visible'); badge.classList.remove('visible'); }
  }, true);

  function select(element) {
    frame(element);
    outline.classList.remove('derived', 'foreign');
    selectedElement = element;
    selectedOrigin = element.getAttribute('data-eq-origin');
    vscode.postMessage({ type: 'select', origin: selectedOrigin });
  }

  document.addEventListener('click', (e) => {
    if (!inspecting || insideChrome(e.target)) return;
    e.preventDefault();
    e.stopPropagation();
    const found = stamped(e.target);
    if (found) select(found);
  }, true);

  /** The nearest stamped ancestor — the node that CONTAINS this one in the tree, skipping the DOM
   * a component builds inside itself. */
  function stampedParent(element) {
    return element.parentElement ? stamped(element.parentElement) : null;
  }

  /**
   * The nearest stamped descendants, one per branch.
   * <para>
   * Not element.children: a component is several elements deep, so its own DOM sits between it and
   * the nodes an author would recognise. This descends until it meets a stamped element and stops
   * there, which is what "the children of this node" means in the tree the author wrote.
   * </para>
   */
  function stampedChildren(element) {
    const found = [];
    const walk = (node) => {
      for (const child of node.children) {
        if (child.getAttribute('data-eq-origin')) found.push(child);
        else walk(child);
      }
    };
    walk(element);
    return found;
  }

  function chip(element, text) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'chip';
    button.textContent = text;
    button.addEventListener('click', () => select(element));
    return button;
  }

  function insertChip(text, index) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'chip add';
    button.textContent = text;
    button.addEventListener('click', () =>
      vscode.postMessage({ type: 'insert', origin: selectedOrigin, index: index }));
    return button;
  }

  function buildTree(node) {
    const strip = document.createElement('div');
    strip.id = 'panel-tree';
    if (!selectedElement || !selectedElement.isConnected) return strip;

    const parent = stampedParent(selectedElement);
    if (parent) {
      strip.appendChild(chip(parent, '\u2191 ' + (parent.getAttribute('data-eq-component') || 'parent')));
    }

    // The insert controls, drawn only where the host has already said there IS a list to insert
    // into. An affordance that appears and then refuses reads as a bug, so the question is asked
    // before the control is drawn rather than after it is pressed.
    const insertable = node && node.childCount >= 0;
    if (insertable) {
      strip.appendChild(insertChip('+ first', 0));
      strip.appendChild(insertChip('+ last', node.childCount));
    } else if (node && node.insertReason) {
      const why = document.createElement('span');
      why.className = 'label';
      why.textContent = '\u2205 ' + node.insertReason;
      why.title = node.insertReason;
      strip.appendChild(why);
    }

    const children = stampedChildren(selectedElement);
    if (children.length === 0) {
      const none = document.createElement('span');
      none.className = 'label';
      none.textContent = parent ? '\u00B7 no children' : 'no children';
      strip.appendChild(none);
      return strip;
    }

    const label = document.createElement('span');
    label.className = 'label';
    label.textContent = '\u00B7 into:';
    strip.appendChild(label);

    // Capped, and the cap is SAID: a table with two hundred rows would otherwise become two hundred
    // chips and bury the properties under them.
    for (const child of children.slice(0, 12)) {
      strip.appendChild(chip(child, child.getAttribute('data-eq-component') || 'child'));
    }
    if (children.length > 12) {
      const more = document.createElement('span');
      more.className = 'label';
      more.textContent = '+' + (children.length - 12) + ' more — click them on the canvas';
      strip.appendChild(more);
    }
    return strip;
  }

  // ---- the properties panel ---------------------------------------------------------------------

  document.getElementById('panel-min').addEventListener('click', () => panel.classList.toggle('minimised'));
  document.getElementById('panel-close').addEventListener('click', () => panel.classList.remove('visible'));

  const SIMPLE_STRING = /^"(?:[^"\\\\]|\\\\.)*"$/;
  const NUMBER = /^-?\\d+(\\.\\d+)?[fFdDmM]?$/;

  /**
   * The control follows what is WRITTEN, not only what the type says. A gap is a float whose value
   * is Space.S4 — a named constant, not a number — so a number box would be a lie about what may
   * be typed there. When the written value is not a literal of the expected shape, the row falls
   * back to editing the C# expression itself, which is always true and never surprising.
   */
  function editorFor(property, commit) {
    const bare = (property.type || '').replace(/\\?$/, '');
    const value = property.value;

    if (property.options && property.options.length) {
      const select = document.createElement('select');
      // An unset property has no value to show; picking one is the point of the row.
      const empty = document.createElement('option');
      empty.value = '';
      empty.textContent = value ? '—' : '(unset)';
      select.appendChild(empty);
      for (const option of property.options) {
        const item = document.createElement('option');
        item.value = option;
        item.textContent = option;
        select.appendChild(item);
      }
      if (value && property.options.indexOf(value) < 0) {
        // Written as something the enum does not spell — an alias, a cast. Shown, never silently
        // replaced by a member that is merely close.
        const written = document.createElement('option');
        written.value = value;
        written.textContent = value;
        select.appendChild(written);
      }
      select.value = value || '';
      select.addEventListener('change', () => { if (select.value) commit(select.value); });
      return select;
    }

    if (bare === 'bool' && (!value || value === 'true' || value === 'false')) {
      const box = document.createElement('input');
      box.type = 'checkbox';
      box.checked = value === 'true';
      box.addEventListener('change', () => commit(box.checked ? 'true' : 'false'));
      return box;
    }

    if (!value || NUMBER.test(value)) {
      const numeric = ['int', 'long', 'short', 'byte', 'float', 'double', 'decimal', 'uint', 'ulong'];
      if (numeric.indexOf(bare) >= 0) {
        const input = document.createElement('input');
        input.type = 'number';
        input.value = value ? value.replace(/[fFdDmM]$/, '') : '';
        commitOn(input, () => input.value.trim());
        return input;
      }
    }

    if (bare === 'string' && (!value || SIMPLE_STRING.test(value))) {
      const input = document.createElement('input');
      input.type = 'text';
      input.value = value ? JSON.parse(value) : '';
      // Quoted on the way out: the panel edits text, and the file takes a C# string literal.
      commitOn(input, () => JSON.stringify(input.value));
      return input;
    }

    const raw = document.createElement('input');
    raw.type = 'text';
    raw.value = value || '';
    raw.title = 'C# expression — written into the file as typed';
    commitOn(raw, () => raw.value.trim());
    return raw;

    function commitOn(element, read) {
      let last = element.value;
      const send = () => {
        if (element.value === last) return;
        last = element.value;
        const written = read();
        if (written) commit(written);
      };
      element.addEventListener('change', send);
      element.addEventListener('keydown', (e) => { if (e.key === 'Enter') { e.preventDefault(); send(); } });
    }
  }

  function showSelection(payload) {
    panelTitle.textContent = payload.node ? payload.node.component : 'Selection';
    panelTier.textContent = payload.tier + (payload.node ? ' · ' + payload.node.form : '');
    panelTier.title = payload.reason || '';
    panel.classList.remove('minimised');
    panel.classList.add('visible');

    panelBody.replaceChildren();
    panelBody.appendChild(buildTree(payload.node));

    if (payload.node && payload.node.summary) {
      const note = document.createElement('div');
      note.id = 'panel-note';
      note.textContent = payload.node.summary;
      panelBody.appendChild(note);
    }

    if (!payload.node) {
      const note = document.createElement('div');
      note.id = 'panel-note';
      note.textContent = payload.reason || 'Nothing to inspect here.';
      panelBody.appendChild(note);
      return;
    }

    const table = document.createElement('table');
    table.className = 'props';

    for (const property of payload.node.properties) {
      // An unset property nobody can reach from this form is noise; an unset one they COULD set is
      // the offer the panel exists to make.
      if (property.kind === 'unset' && !property.editable && !property.reason) continue;

      const row = document.createElement('tr');
      const name = document.createElement('td');
      name.className = 'pname';
      name.textContent = property.name;
      if (property.summary) name.title = property.summary;

      const type = document.createElement('td');
      type.className = 'ptype';
      type.textContent = property.type;

      const value = document.createElement('td');
      value.className = 'pvalue';

      if (!property.editable) {
        row.className = 'unreachable';
        value.textContent = property.reason || 'not reachable from this form';
        value.title = value.textContent;
      } else {
        const origin = selectedOrigin;
        value.appendChild(editorFor(property, (written) =>
          vscode.postMessage({ type: 'edit', origin: origin, property: property.name, value: written })));
      }

      row.append(name, type, value);
      table.appendChild(row);
    }

    panelBody.appendChild(table);
  }

  // Announced only after the listeners above exist. A message posted before this point is simply
  // dropped by the webview, which is how a first render can go missing and the panel sit empty until
  // the next keystroke.
  vscode.postMessage({ type: 'ready' });

  window.addEventListener('message', (event) => {
    const payload = event.data;
    if (payload.type === 'render') {
      // The outline framed an element of the PREVIOUS tree; after a recompile that rectangle means
      // nothing. The panel stays: an edit made from it recompiles, and closing the panel under the
      // hand that just used it would be its own bug.
      outline.classList.remove('visible');
      badge.classList.remove('visible');
      void render(payload);
    } else if (payload.type === 'selected') {
      if (payload.tier !== 'literal') outline.classList.add(payload.tier);
      showSelection(payload);
    } else if (payload.type === 'inspect') {
      setInspecting(payload.on);
    } else if (payload.type === 'stale') {
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
