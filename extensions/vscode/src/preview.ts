import * as vscode from 'vscode';
import { CompileResult, EditResult, OpenBuffer, PaletteEntry, Sidecar } from './sidecar';
import { LayerNode, LayersProvider } from './layers';
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
    private readonly layers: LayersProvider,
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

  /** A node picked in the layers list — sent to the canvas, which selects it the way a click does:
   * the outline, the panel and the editor's cursor all follow from the one path. */
  revealNode(origin: string): void {
    this.post({ type: 'reveal', origin });
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
    this.post({
      type: 'render', js: result.js, className: result.className, theme: this.theme,
      runtime: this.runtimeUri, target: this.layout.target,
    });
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
      nodes?: unknown[]; truncated?: boolean;
      options?: string[]; index?: number; delta?: number; target?: string; list?: string; on?: boolean;
      elementType?: string;
    },
  ): void {
    if (message.type === 'ready') this.announceReady();
    else if (message.type === 'tree' && Array.isArray(message.nodes)) {
      this.layers.update(message.nodes as LayerNode[], message.truncated === true);
    }
    else if (message.type === 'threw') this.log(`preview threw: ${message.detail ?? ''}`);
    else if (message.type === 'select' && message.origin) void this.revealSource(message.origin);
    else if (message.type === 'edit' && message.origin && message.property) {
      void this.editProperty(message.origin, message.property, message.value, message.options);
    } else if (message.type === 'insert' && message.origin && typeof message.index === 'number') {
      void this.insertChild(message.origin, message.index, message.list, message.elementType);
    } else if (message.type === 'duplicate' && message.origin) {
      void this.duplicate(message.origin);
    } else if (message.type === 'inspectMode') {
      this.setInspecting(message.on === true);
    } else if (message.type === 'unset' && message.origin && message.property) {
      void this.unsetProperty(message.origin, message.property);
    } else if (message.type === 'removeAt' && message.origin && message.list && typeof message.index === 'number') {
      void this.removeAt(message.origin, message.list, message.index);
    } else if (message.type === 'arrange' && message.origin) {
      void this.arrange(message.origin, message.delta);
    } else if (message.type === 'reorder' && message.origin && typeof message.index === 'number') {
      void this.reorder(message.origin, message.index);
    } else if (message.type === 'moveTo' && message.origin && message.target && typeof message.index === 'number') {
      void this.moveAcross(message.origin, message.target, message.index);
    } else if (message.type === 'ask' && message.origin) {
      void this.answer(message.origin);
    }
  }

  /**
   * What the canvas needs to know before it draws anything on a node: does it sit in a list, where,
   * and does it hold one.
   * <para>
   * Cheaper than <c>describe</c> — no tier, no panel — because this one is asked on HOVER. The
   * webview caches the answer per origin and per compile, so a pointer crossing the same row twice
   * costs one round trip, not two, and an affordance is only ever drawn where the host has already
   * said it would work.
   * </para>
   */
  private async answer(origin: string): Promise<void> {
    const span = PreviewPanel.parseOrigin(origin);
    if (!span) return;

    try {
      const node = await this.sidecar.inspect(span.path, await this.textOf(span.path), origin, this.buffers());
      this.post({ type: 'known', origin, node: node === '' ? undefined : node });
    } catch (error) {
      this.log(`ask failed: ${(error as Error).message}`);
    }
  }

  /**
   * A drag that left its own container: the node joins a different list.
   * <para>
   * A removal and an insertion, and therefore the first edit with two ends — written by the host as
   * ONE replacement spanning both, so it is still one Ctrl+Z. What may be carried across is decided
   * by the compiler: a node written against a local of the method it came from does not compile in
   * its new home, and the refusal quotes that rather than inventing a rule.
   * </para>
   */
  private async moveAcross(origin: string, target: string, index: number): Promise<void> {
    const span = PreviewPanel.parseOrigin(origin);
    if (!span) return;

    try {
      const edit = await this.sidecar.moveAcross(
        span.path, await this.textOf(span.path), origin, target, index, this.buffers());
      await this.applyMove(span.path, origin, edit, 'that move was refused');
    } catch (error) {
      this.log(`move failed: ${(error as Error).message}`);
    }
  }

  /**
   * A drag, landed: the node goes to a position, however far away it is.
   * <para>
   * One edit, not a run of swaps — a gesture the hand made once should be a gesture Ctrl+Z undoes
   * once.
   * </para>
   */
  private async reorder(origin: string, index: number): Promise<void> {
    const span = PreviewPanel.parseOrigin(origin);
    if (!span) return;

    try {
      const edit = await this.sidecar.reorderChild(
        span.path, await this.textOf(span.path), origin, index, this.buffers());
      await this.applyMove(span.path, origin, edit, 'that move was refused');
    } catch (error) {
      this.log(`reorder failed: ${(error as Error).message}`);
    }
  }

  /**
   * Move a child among its siblings, or take it out.
   * <para>
   * Both go through the same path as every other edit: one WorkspaceEdit, so one Ctrl+Z reverses the
   * gesture, and the host refuses rather than writing anything that would not compile.
   * </para>
   */
  private async arrange(origin: string, delta: number | undefined): Promise<void> {
    const span = PreviewPanel.parseOrigin(origin);
    if (!span) return;

    try {
      const text = await this.textOf(span.path);
      const buffers = this.buffers();
      if (delta === undefined) {
        const edit = await this.sidecar.removeChild(span.path, text, origin, buffers);
        if (!(await this.applyEdit(span.path, edit, 'that was refused'))) return;
        // A removed node has no origin left to describe, so the panel closes rather than showing an
        // answer about something that is no longer there.
        this.post({ type: 'deselect' });
        return;
      }

      const moved = await this.sidecar.moveChild(span.path, text, origin, delta, buffers);
      await this.applyMove(span.path, origin, moved, 'that was refused');
    } catch (error) {
      this.log(`arrange failed: ${(error as Error).message}`);
    }
  }

  /** Applies an edit, or shows the host's own sentence explaining why there is none. */
  private async applyEdit(path: string, edit: EditResult, fallback: string): Promise<boolean> {
    if (!edit.applied) {
      void vscode.window.showWarningMessage(`eQuantic UI: ${edit.reason ?? fallback}`);
      return false;
    }

    const workspaceEdit = new vscode.WorkspaceEdit();
    workspaceEdit.replace(
      vscode.Uri.file(path),
      new vscode.Range(edit.startLine, edit.startColumn, edit.endLine, edit.endColumn),
      edit.newText);

    if (!(await vscode.workspace.applyEdit(workspaceEdit))) return false;
    this.settle();
    return true;
  }

  /**
   * The window between an applied edit and the frame that reflects it.
   * <para>
   * Every origin in the rendered tree names a span in the text as it WAS. An edit that adds or removes
   * lines moves everything between its two ends, so until the next render a click on the canvas asks
   * about coordinates that now hold a different node — and answers about it, confidently. The canvas
   * is told to stop offering anything origin-derived, and the recompile is brought forward from the
   * typing debounce so the pause is as short as a compile.
   * </para>
   */
  private settle(): void {
    this.post({ type: 'settling' });
    if (this.compileTimer) clearTimeout(this.compileTimer);
    if (this.diagnoseTimer) clearTimeout(this.diagnoseTimer);
    this.compileTimer = undefined;
    this.diagnoseTimer = undefined;
    void this.publishDiagnostics();
    void this.refresh();
  }

  /**
   * The panel goes on talking about the SAME node after an edit inside it.
   * <para>
   * An origin is a span, and an edit within a node changes its length. Unsetting a member shortens it,
   * so the span the panel is holding runs past its end — and a span that overshoots resolves to the
   * smallest node CONTAINING it, which is the parent. Without this the panel would quietly start
   * describing, and then editing, the container of the thing it was asked about.
   * </para>
   */
  private async follow(origin: string, edit: EditResult): Promise<void> {
    const landed = edit.origin ?? origin;
    if (landed !== origin) this.post({ type: 'reselect', origin: landed });
    await this.describe(landed);
  }

  /**
   * An edit that MOVED the selected node, followed by the selection.
   * <para>
   * The origin the panel was holding is a span, and after the move those coordinates hold the sibling
   * it was swapped with — so describing the old one would quietly switch which node the panel is
   * about. The host says where the node landed; the canvas and the panel both follow it there.
   * </para>
   */
  private async applyMove(path: string, origin: string, edit: EditResult, fallback: string): Promise<void> {
    if (!(await this.applyEdit(path, edit, fallback))) return;

    const landed = edit.origin ?? origin;
    this.post({ type: 'reselect', origin: landed });
    await this.describe(landed);
  }

  /**
   * Cached for the session, by what the list HOLDS: the surface does not change while the editor is
   * open, and a quick pick that waits on a round trip feels like a stall. Keyed by element type
   * because a Grid's `columns` is offered GridTracks and a Column's `children` is offered components.
   */
  private components: PaletteEntry[] | undefined;

  private async paletteFor(
    origin: string, list: string | undefined, elementType: string | undefined,
  ): Promise<PaletteEntry[]> {
    // Only the COMPONENT surface is cached. A value palette is resolved from the origin, so a stale
    // one falls back to the component surface — and a fallback cached under the element type would
    // then answer for that type for the rest of the session. It is one round trip on an explicit
    // click; the surface is the one worth keeping.
    if (elementType === undefined && this.components) return this.components;

    const span = PreviewPanel.parseOrigin(origin);
    const entries = await this.sidecar.palette(
      span?.path, span ? await this.textOf(span.path) : undefined, origin, list);

    if (elementType === undefined) this.components = entries;
    return entries;
  }

  /** A copy of the selected node, and the copy is what stays selected — it is the thing just made. */
  private async duplicate(origin: string): Promise<void> {
    const span = PreviewPanel.parseOrigin(origin);
    if (!span) return;

    try {
      const edit = await this.sidecar.duplicateChild(
        span.path, await this.textOf(span.path), origin, this.buffers());
      await this.applyMove(span.path, origin, edit, 'that could not be duplicated');
    } catch (error) {
      this.log(`duplicate failed: ${(error as Error).message}`);
    }
  }

  /** Takes a member back out of a value's initializer. A sheet you can only add rows to leaks. */
  private async unsetProperty(origin: string, property: string): Promise<void> {
    const span = PreviewPanel.parseOrigin(origin);
    if (!span) return;

    try {
      const edit = await this.sidecar.unsetProperty(
        span.path, await this.textOf(span.path), origin, property, this.buffers());
      if (await this.applyEdit(span.path, edit, 'that was refused')) await this.follow(origin, edit);
    } catch (error) {
      this.log(`unset failed: ${(error as Error).message}`);
    }
  }

  /** Removes an entry of a data list, which is addressed by position because it has no origin. */
  private async removeAt(origin: string, list: string, index: number): Promise<void> {
    const span = PreviewPanel.parseOrigin(origin);
    if (!span) return;

    try {
      const edit = await this.sidecar.removeAt(
        span.path, await this.textOf(span.path), origin, list, index, this.buffers());
      if (await this.applyEdit(span.path, edit, 'that was refused')) await this.follow(origin, edit);
    } catch (error) {
      this.log(`remove failed: ${(error as Error).message}`);
    }
  }

  /**
   * Insert a component into a declarative children list.
   * <para>
   * Offered only where the host has already said there IS such a list — an affordance that appears
   * and then refuses reads as a bug, so the panel asks before it draws the control rather than after
   * it is pressed.
   * </para>
   */
  private async insertChild(
    origin: string, index: number, list?: string, elementType?: string,
  ): Promise<void> {
    const span = PreviewPanel.parseOrigin(origin);
    if (!span) return;

    try {
      const palette = await this.paletteFor(origin, list, elementType);

      // Grouped, with the app's own components first and under their own heading. They are what the
      // developer was writing a minute ago, and a flat list buries them among a hundred framework
      // names — searchable, but only if you already know what you are looking for.
      const items: (vscode.QuickPickItem & { entry?: PaletteEntry })[] = [];
      let heading = '';
      for (const entry of palette) {
        if (entry.source !== heading) {
          heading = entry.source;
          items.push({
            label: entry.source === 'app' ? 'This app' : entry.source === 'value' ? 'Values' : 'Framework',
            kind: vscode.QuickPickItemKind.Separator,
          });
        }
        items.push({ label: entry.name, description: entry.snippet, detail: entry.summary ?? undefined, entry });
      }

      const pick = await vscode.window.showQuickPick(items, {
        title: list && list !== 'children' ? `Add to ${list}` : 'Insert a component',
        matchOnDescription: true,
        matchOnDetail: true,
      });
      if (!pick?.entry) return;

      const edit = await this.sidecar.insertChild(
        span.path, await this.textOf(span.path), origin, index, pick.entry.snippet, this.buffers(), list);

      if (await this.applyEdit(span.path, edit, 'that insertion was refused')) await this.follow(origin, edit);
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
      this.layers.select(origin);
      this.post({
        type: 'selected',
        origin,
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
  body { display: flex; flex-direction: column; }

  /* ---- the toolbar ----------------------------------------------------------------------------
     A toolbar of our own rather than more buttons in VS Code's title bar. The title bar can hold
     commands and nothing else: a form-factor selector is a control with STATE, and expressing it as
     two commands that swap places is a trick that stops working at the third format. Built from the
     editor's own variables, so it follows the theme without knowing which one is on. */
  #bar {
    flex: none; display: flex; align-items: center; gap: 6px; padding: 0 8px;
    height: 30px; box-sizing: border-box;
    background: var(--vscode-editorWidget-background, var(--vscode-panel-background, #252526));
    border-bottom: 1px solid var(--vscode-panel-border, rgba(128,128,128,0.35));
    color: var(--vscode-foreground, #ccc);
    font: 12px/1.4 var(--vscode-font-family, sans-serif);
  }
  #bar button.icon {
    display: flex; align-items: center; justify-content: center;
    width: 24px; height: 22px; padding: 0; border: none; border-radius: 5px;
    background: transparent; color: var(--vscode-icon-foreground, #c5c5c5); cursor: pointer;
  }
  #bar button.icon:hover { background: var(--vscode-toolbar-hoverBackground, rgba(90,93,94,0.31)); }
  #bar button.icon:focus-visible { outline: 1px solid var(--vscode-focusBorder, #0078d4); }
  /* PRESSED, not merely hovered: inspect is a mode, and a mode that looks like a button is a mode
     you cannot tell you are in. */
  #bar button.icon[aria-pressed="true"] {
    background: var(--vscode-inputOption-activeBackground, rgba(0,120,212,0.4));
    color: var(--vscode-inputOption-activeForeground, #fff);
    box-shadow: inset 0 0 0 1px var(--vscode-inputOption-activeBorder, transparent);
  }
  #bar .sep {
    width: 1px; height: 16px; margin: 0 2px;
    background: var(--vscode-panel-border, rgba(128,128,128,0.35));
  }
  #bar select {
    background: var(--vscode-dropdown-background, #3c3c3c);
    color: var(--vscode-dropdown-foreground, #ccc);
    border: 1px solid var(--vscode-dropdown-border, rgba(128,128,128,0.4));
    border-radius: 3px; font: inherit; padding: 1px 4px; max-width: 190px;
  }
  #bar .grow { flex: 1 1 auto; }
  #bar .note {
    color: var(--vscode-descriptionForeground, #9d9d9d); white-space: nowrap;
    overflow: hidden; text-overflow: ellipsis;
  }

  /* ---- the stage ------------------------------------------------------------------------------
     Where the screen is shown at the size the format asks for. It scrolls, because a phone is taller
     than the panel usually is. */
  #stage {
    flex: 1 1 auto; overflow: auto; display: flex; justify-content: center;
    background: var(--eq-preview-bg, Canvas);
  }
  #stage.framed {
    align-items: flex-start; padding: 14px 0;
    /* Not the app's own background: a device sitting ON something reads as a device. */
    background: var(--vscode-editorWidget-background, #252526);
  }
  #frame { flex: 1 1 auto; display: flex; min-height: 100%; }
  /* A shell, not a picture of a phone: a rounded bezel with room for the screen inside it. */
  #stage.framed #frame {
    flex: none; min-height: 0; padding: 10px;
    border-radius: 26px; background: #1c1c1e;
    box-shadow: 0 0 0 1px rgba(255,255,255,0.08), 0 10px 30px rgba(0,0,0,0.45);
  }
  #app { flex: 1 1 auto; box-sizing: border-box; }
  #stage.framed #app {
    flex: none; overflow: auto; border-radius: 18px;
    background: var(--eq-preview-bg, Canvas);
  }

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

  /* Canvas affordances. The caret is a MARK and takes no pointer events; the chips are targets and
     are part of the chrome, so hovering one does not count as leaving the node it belongs to. */
  #caret {
    position: fixed; z-index: 5; display: none; pointer-events: none; border-radius: 2px;
    background: var(--vscode-focusBorder, #0078d4);
  }
  #caret.visible { display: block; }
  /* Over a pile the mark is a COVER, not a line: it says which child the node would come to rest in
     front of, because between two overlapping children there is no gap a line could sit in. */
  #caret.layer {
    background: color-mix(in srgb, var(--vscode-focusBorder, #0078d4) 22%, transparent);
    border: 1px solid var(--vscode-focusBorder, #0078d4);
    border-radius: 3px;
  }

  /* Where a component COULD go. Dashed rather than solid, because it marks a possibility and the
     caret marks a commitment — the eye should not have to read a label to tell them apart. */
  #gap {
    position: fixed; z-index: 5; display: none; pointer-events: none; border-radius: 2px;
    background: repeating-linear-gradient(90deg,
      var(--vscode-focusBorder, #0078d4) 0 5px, transparent 5px 9px);
  }
  #gap.vertical {
    background: repeating-linear-gradient(180deg,
      var(--vscode-focusBorder, #0078d4) 0 5px, transparent 5px 9px);
  }
  #gap.visible { display: block; }

  .eq-plus {
    position: fixed; z-index: 5; display: none; box-sizing: border-box;
    width: 18px; height: 18px; padding: 0; border-radius: 9px; cursor: pointer;
    align-items: center; justify-content: center;
    font: 12px/1 var(--vscode-font-family, sans-serif);
    border: 1px solid var(--vscode-focusBorder, #0078d4);
    background: var(--vscode-editor-background, #1e1e1e);
    color: var(--vscode-focusBorder, #0078d4);
  }
  .eq-plus.visible { display: flex; }
  .eq-plus:hover { background: var(--vscode-focusBorder, #0078d4); color: var(--vscode-button-foreground, #fff); }
  .eq-plus:focus-visible { outline: 1px solid var(--vscode-focusBorder, #0078d4); }

  /* On every element, not only on body: the app draws its own cursors, and a button under the hand
     that still says "click me" mid-drag is telling the truth about the wrong gesture. */
  body.eq-dragging, body.eq-dragging * { cursor: grabbing !important; }

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
  /* Hidden rather than disabled when the node is not an element of a list: there is nothing to
     explain about an arrangement that does not exist. */
  #panel button.icon[hidden] { display: none; }
  #panel button.icon:disabled { opacity: 0.35; cursor: default; }
  #panel button.icon:disabled:hover { background: transparent; }

  #panel-body { overflow: auto; padding: 6px 10px 10px; }

  /* The lists a call is written with that are NOT children: a Grid's columns, a menu's items. Their
     entries never render, so the canvas cannot point at them and the panel is the only place they
     can be reached from. */
  .eq-list { display: flex; flex-wrap: wrap; gap: 4px; align-items: center; padding: 0 0 6px; }
  .eq-list .lname {
    font-weight: 600; margin-right: 2px;
    color: var(--vscode-symbolIcon-propertyForeground, var(--vscode-foreground, #ccc));
  }
  .eq-list .entry {
    display: inline-flex; align-items: center; gap: 4px;
    padding: 1px 4px 1px 7px; border-radius: 10px;
    border: 1px solid var(--vscode-panel-border, rgba(128,128,128,0.4));
    font: 11px/1.6 var(--vscode-editor-font-family, ui-monospace, monospace);
  }
  .eq-list .entry button {
    border: none; background: transparent; cursor: pointer; padding: 0 2px; border-radius: 8px;
    color: var(--vscode-descriptionForeground, #9d9d9d); font: inherit;
  }
  /* The members of a value, as a sheet under the property that carries them. Indented and ruled on
     the left, so the eye reads them as INSIDE the row above rather than as more properties. */
  .members-row > td { padding: 0 0 6px 10px; }
  .members {
    width: 100%; border-collapse: collapse;
    border-left: 2px solid var(--vscode-panel-border, rgba(128,128,128,0.45));
  }
  .members td { padding: 1px 6px; vertical-align: top; }
  .members select {
    background: var(--vscode-dropdown-background, #3c3c3c);
    color: var(--vscode-dropdown-foreground, #ccc);
    border: 1px solid var(--vscode-dropdown-border, rgba(128,128,128,0.4));
    border-radius: 3px; font: inherit; padding: 0 2px;
  }
  .members .pdrop { width: 18px; text-align: right; }
  .members .pdrop button {
    border: none; background: transparent; cursor: pointer; border-radius: 8px; font: inherit;
    color: var(--vscode-descriptionForeground, #9d9d9d); padding: 0 4px;
  }
  .members .pdrop button:hover {
    background: var(--vscode-toolbar-hoverBackground, rgba(90,93,94,0.31));
    color: var(--vscode-errorForeground, #e5645c);
  }
  .props .pvalue.count { color: var(--vscode-descriptionForeground, #9d9d9d); font-style: italic; }

  .eq-list .entry button:hover {
    background: var(--vscode-toolbar-hoverBackground, rgba(90,93,94,0.31));
    color: var(--vscode-errorForeground, #e5645c);
  }
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
<div id="bar">
  <button class="icon" id="bar-inspect" type="button" aria-pressed="false"
          title="Inspect elements" aria-label="Inspect elements">
    <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
      <path d="M3.5 2.5l9 4.2-3.7 1.2-1.2 3.7-4.1-9.1z" stroke="currentColor" stroke-width="1.2"
            stroke-linejoin="round"/>
    </svg>
  </button>
  <span class="sep"></span>
  <select id="bar-format" title="The size the screen is shown at"></select>
  <span class="grow"></span>
  <span class="note" id="bar-note"></span>
</div>
<div id="stage"><div id="frame"><div id="app"></div></div></div>
<div id="outline"></div>
<div id="badge"></div>
<div id="caret"></div>
<div id="gap"></div>
<button class="eq-plus" id="plus-before" type="button" title="Insert before this" aria-label="Insert before this">+</button>
<button class="eq-plus" id="plus-after" type="button" title="Insert after this" aria-label="Insert after this">+</button>

<div id="panel">
  <div id="panel-head">
    <span id="panel-title"></span>
    <span id="panel-tier"></span>
    <span id="panel-spacer"></span>
    <button class="icon" id="panel-up" type="button" title="Move up" aria-label="Move up">
      <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
        <path d="M8 12V4M4.5 7.5L8 4l3.5 3.5" stroke="currentColor" stroke-width="1.2" stroke-linecap="round" stroke-linejoin="round"/>
      </svg>
    </button>
    <button class="icon" id="panel-down" type="button" title="Move down" aria-label="Move down">
      <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
        <path d="M8 4v8M4.5 8.5L8 12l3.5-3.5" stroke="currentColor" stroke-width="1.2" stroke-linecap="round" stroke-linejoin="round"/>
      </svg>
    </button>
    <button class="icon" id="panel-remove" type="button" title="Remove" aria-label="Remove">
      <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
        <path d="M3.5 4.5h9M6.5 4.5V3h3v1.5M5 4.5l.6 8h4.8l.6-8" stroke="currentColor" stroke-width="1.2" stroke-linecap="round" stroke-linejoin="round"/>
      </svg>
    </button>
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
  const caret = document.getElementById('caret');
  const stage = document.getElementById('stage');
  const device = document.getElementById('frame');
  const formatSelect = document.getElementById('bar-format');
  const note = document.getElementById('bar-note');
  const inspectButton = document.getElementById('bar-inspect');
  /** Where the gate overrides live — later than the runtime's own sheet, so they win. */
  const gates = document.createElement('style');
  document.head.appendChild(gates);
  const gapMark = document.getElementById('gap');
  const plusBefore = document.getElementById('plus-before');
  const plusAfter = document.getElementById('plus-after');
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

  /** The last frame that mounted. A format change re-mounts it: density is read while a component
   * BUILDS, so it is baked into the tree and a stylesheet cannot change it afterwards. */
  let lastFrame = null;
  /** The instance on screen — kept so its state can be carried into the next mount. */
  let mountedInstance = null;
  let mountedClass = '';

  /**
   * The instance fields the framework did not put there, probed rather than listed.
   *
   * A write-once page keeps no _state bag: its C# fields compile to plain instance fields, so the
   * only way to tell the author's _count from the base class's _renderManager is to ask the base
   * what IT contributes — a sacrificial instance of each exported base, keys unioned. A hand-written
   * list of internals would be stale on the first field the runtime grows.
   */
  function probeFrameworkKeys() {
    if (!mountedInstance || !mountedInstance.constructor) return null;
    const Base = Object.getPrototypeOf(mountedInstance.constructor);
    if (typeof Base !== 'function' || !Base.prototype) return null;

    // The keys the page's OWN base contributes, probed off a sacrificial instance — constructed AND
    // mounted, because half of them (_lifecycleMounted, the invalidation hook) only appear at mount,
    // and carrying _lifecycleMounted=true into a fresh instance silences its OnMount. The base is
    // taken from the instance's prototype chain rather than from a runtime export, because guessing
    // the export probes a DIFFERENT class and lets every one of its keys through.
    const keys = new Set(['_state']);
    try {
      const probe = new (class extends Base { build() { return null; } getVirtualNode() { return null; } })();
      for (const key of Object.keys(probe)) keys.add(key);
      try {
        probe.mount(document.createElement('div'));
      } catch (ignored) { /* a null build may refuse to mount; construction keys still count */ }
      for (const key of Object.keys(probe)) keys.add(key);
    } catch (ignored) {
      return null;
    }
    return keys;
  }

  /**
   * What the page on screen holds: the author's own instance fields, plus the _state bag when there
   * is one (Core stateful pages). Functions never travel — a captured closure would still see the
   * OLD module.
   */
  function captureState() {
    try {
      if (!mountedInstance) return null;
      const frameworkKeys = probeFrameworkKeys();
      if (!frameworkKeys) return null;
      const data = {};
      let any = false;

      for (const key of Object.keys(mountedInstance)) {
        const value = mountedInstance[key];
        if (frameworkKeys.has(key) || typeof value === 'function') continue;
        data[key] = value;
        any = true;
      }

      const state = mountedInstance._state;
      if (state) {
        for (const key of Object.keys(state)) {
          const value = state[key];
          if (typeof value === 'function') continue;
          if (key === '_component' || key === '_context' || key === '_needsRender') continue;
          data[key] = value;
          any = true;
        }
      }

      return any ? data : null;
    } catch (ignored) {
      // Carrying nothing is a fresh mount, which is what every recompile was until now.
      return null;
    }
  }

  async function render(payload) {
    notice.classList.remove('visible');
    lastFrame = payload;
    adoptTarget(payload.target);

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
        // The FORMAT decides, when one has been chosen: a phone is a touch target and its controls
        // are the full-size ones, whatever pointer the machine running the preview happens to have.
        if (format().density) runtime.setPhotonDensity(format().density);
        else runtime.detectPhotonDensity();
        runtime.setPhotonTheme(runtime.materializeTheme(theme));
        paintBackground(theme);
      }
      const Component = loaded[payload.className];
      if (typeof Component !== 'function') {
        report('Nothing to mount:', 'the compiled module has no export named ' + payload.className);
        return;
      }
      // What the outgoing page held, re-entering through the SAME door the SSR handoff uses
      // (__INITIAL_STATE__ -> adoptServerState / the stateful page's own merge). Guarded by class
      // name the way the boot guards by URL: a renamed page must start fresh, not inherit fields
      // from a stranger that happens to spell them the same.
      const carried = mountedClass === payload.className ? captureState() : null;
      if (carried) window.__INITIAL_STATE__ = carried;

      app.replaceChildren();
      mountedInstance = new Component();
      mountedClass = payload.className;
      mountedInstance.mount(app);
      // Consumed once, whoever consumed it: a payload left behind would leak into the NEXT page
      // someone previews in this panel.
      delete window.__INITIAL_STATE__;
      // After mount: the stamped elements only exist once the component has rendered, and the gates
      // only exist once something has asked for them.
      describeTree();
      applyWidth();
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

  inspectButton.addEventListener('click', () => {
    // Asks rather than sets: the extension owns the mode, because the title bar's buttons and the
    // context key behind them are its. It answers with an inspect message, and that is what moves
    // this button — so the two can never disagree about which mode we are in.
    vscode.postMessage({ type: 'inspectMode', on: !inspecting });
  });

  function setInspecting(on) {
    inspecting = on;
    inspectButton.setAttribute('aria-pressed', on ? 'true' : 'false');
    if (!on) {
      outline.classList.remove('visible');
      badge.classList.remove('visible');
      // Every affordance goes with the mode. One left behind is a control that acts on a tree the
      // pointer is no longer allowed to touch.
      hideInserts();
      caret.classList.remove('visible');
      drag = null;
      document.body.classList.remove('eq-dragging');
    }
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
    return panel.contains(target) || plusBefore.contains(target) || plusAfter.contains(target);
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
      if (name === 'pointerdown') beginDrag(e, stamped(e.target));
    }, true);
  }

  document.addEventListener('pointermove', (e) => {
    if (!inspecting) return;
    if (drag) { updateDrag(e); return; }
    if (insideChrome(e.target)) return;
    const found = stamped(e.target);
    if (found) { frame(found); hovering(found, e); }
    else { outline.classList.remove('visible'); badge.classList.remove('visible'); hideInserts(); }
  }, true);

  document.addEventListener('pointerup', (e) => {
    if (!inspecting) return;
    if (!endDrag()) return;
    // A drag ends with a click event on whatever is under the pointer. Selecting there would mean
    // every drop also re-selected something, sometimes the node it was dropped onto.
    suppressClick = true;
    e.preventDefault();
    e.stopPropagation();
  }, true);

  function select(element) {
    frame(element);
    outline.classList.remove('derived', 'foreign');
    selectedElement = element;
    selectedOrigin = element.getAttribute('data-eq-origin');
    vscode.postMessage({ type: 'select', origin: selectedOrigin });
  }

  let suppressClick = false;

  document.addEventListener('click', (e) => {
    if (!inspecting || insideChrome(e.target)) return;
    e.preventDefault();
    e.stopPropagation();
    if (suppressClick) { suppressClick = false; return; }
    // Selecting on a stale tree reveals, and then EDITS, whatever moved into those coordinates.
    if (settling) return;
    const found = stamped(e.target);
    if (found) select(found);
  }, true);

  // ---- the format ------------------------------------------------------------------------------
  //
  // The framework has exactly two responsive axes, and a format is a choice on both of them:
  // WindowSizeClass (Compact / Medium / Expanded, at 600dp and 840dp) and Density (Comfortable for
  // touch, Compact for a pointer). Naming the presets after devices is for the reader; what is
  // actually applied is those two, which is why the bar says which.
  const FORMATS = [
    { id: 'fill', label: 'Fill the panel', width: 0, height: 0, density: null, shell: false },
    { id: 'phone', label: 'Phone \u00B7 390 \u00D7 844', width: 390, height: 844, density: 'comfortable', shell: true },
    { id: 'phone-max', label: 'Phone L \u00B7 430 \u00D7 932', width: 430, height: 932, density: 'comfortable', shell: true },
    { id: 'tablet', label: 'Tablet \u00B7 834 \u00D7 1112', width: 834, height: 1112, density: 'comfortable', shell: true },
    { id: 'desktop', label: 'Desktop \u00B7 1280 \u00D7 800', width: 1280, height: 800, density: 'compact', shell: false },
  ];

  let formatId = 'fill';
  /** What the project builds FOR — arrives with the first frame, decides the starting format. */
  let projectTarget = 'unknown';
  /** Once someone has chosen a format, the project's default stops overriding them. */
  let formatChosen = false;

  function format() {
    return FORMATS.find((entry) => entry.id === formatId) || FORMATS[0];
  }

  /**
   * A NATIVE project opens in the phone shell, because that is the shape it ships in — showing it
   * stretched across a desktop panel first would be the preview leading with a layout the app never
   * has. The choice is a default, not a cage: the selector still offers everything, and once a hand
   * has touched it the project stops having an opinion.
   */
  function adoptTarget(target) {
    if (target) projectTarget = target;
    if (projectTarget === 'native' && !formatChosen && formatId === 'fill') {
      formatId = 'phone';
      formatSelect.value = formatId;
      applyFormat();
    } else {
      note.textContent = noteText();
      note.title = noteTitle();
    }
  }

  /** What the framework would call this width — its own vocabulary, not a device name. */
  function sizeClass(width) {
    if (width <= 0) return null;
    return width < 600 ? 'compact' : width < 840 ? 'medium' : 'expanded';
  }

  /**
   * The adaptive gates, resolved against the CHOSEN width instead of the window's.
   *
   * This is what makes a phone-sized preview honest. An AdaptiveNode emits every variant it declares,
   * each wrapped in a gate class whose rules are VIEWPORT media queries — so a 390px box inside a
   * wide panel still shows the expanded variant, which is the one design deviation a device frame
   * would otherwise hide. The range is IN the gate's name (eq-vc600, eq-vm600-840, eq-vx1024),
   * so the same decision can be made here, written as plain rules in a sheet that comes later and
   * therefore wins.
   */
  function applyWidth() {
    const width = format().width;
    const classes = new Set();
    for (const element of app.querySelectorAll('[class*="eq-v"]')) {
      for (const name of element.classList) {
        if (/^eq-v[cmx][0-9.]+(-[0-9.]+)?$/.test(name)) classes.add(name);
      }
    }

    const rules = [];
    for (const gate of classes) {
      const kind = gate[4];
      const range = gate.slice(5).split('-').map(Number);
      const shown = width <= 0
        ? null
        : kind === 'c'
          ? width < range[0]
          : range.length > 1
            ? width >= range[0] && width < range[1]
            : width >= range[0];
      // Left to the stylesheet the runtime wrote when no width is being simulated.
      if (shown !== null) rules.push('.' + gate + '{display:' + (shown ? 'contents' : 'none') + '}');
    }

    // Joined with a SPACE. A newline written here is eaten by the template literal and arrives
    // as a real line break inside the string, which does not parse — CSS does not care either way.
    gates.textContent = rules.join(' ');
  }

  function applyFormat() {
    const chosen = format();
    stage.classList.toggle('framed', chosen.shell);
    device.classList.toggle('phone', chosen.shell);

    app.style.width = chosen.width > 0 ? chosen.width + 'px' : '';
    app.style.height = chosen.height > 0 ? chosen.height + 'px' : '';

    note.textContent = noteText();
    note.title = noteTitle();

    applyWidth();
    // Density is read while a component BUILDS, so it is baked into the tree: a stylesheet cannot
    // change it and only a re-mount can.
    if (lastFrame) void render(lastFrame);
  }

  function noteText() {
    const at = sizeClass(format().width);
    const project = projectTarget === 'native' ? 'native project' : projectTarget === 'web' ? 'web project' : '';
    const sized = at ? at + ' width \u00B7 ' + format().density + ' density \u00B7 web realizer' : '';
    return project && sized ? project + ' \u00B7 ' + sized : project || sized;
  }

  function noteTitle() {
    if (projectTarget !== 'native') return noteText() ? 'Rendered by the web realizer.' : '';
    return 'This project ships through the native (Photon) engine. The preview renders the SAME '
      + 'abstract component tree through the web realizer, at the size class and density a target of '
      + 'this shape would have \u2014 it is not a frame from the native engine.';
  }

  formatSelect.addEventListener('change', () => {
    formatChosen = true;
    formatId = formatSelect.value;
    applyFormat();
  });

  for (const entry of FORMATS) {
    const option = document.createElement('option');
    option.value = entry.id;
    option.textContent = entry.label;
    formatSelect.appendChild(option);
  }
  formatSelect.value = formatId;

  // ---- the keyboard --------------------------------------------------------------------------
  //
  // A visual editor that can only be driven with a pointer is a visual editor you have to keep
  // reaching across the desk for. Everything here is a gesture that already exists — the keys are
  // another way to ask for it, not a second implementation of it.
  //
  // Arrows WALK the tree (up to the parent, down to the first child, left and right along the
  // siblings), Alt+arrows MOVE the node instead, Delete removes it, Cmd/Ctrl+D duplicates it, and
  // Escape lets go — first of the selection, then of the mode.
  window.addEventListener('keydown', (event) => {
    if (!inspecting || insideChrome(event.target)) return;

    const stop = () => { event.preventDefault(); event.stopPropagation(); };

    if (event.key === 'Escape') {
      stop();
      if (selectedOrigin) {
        selectedOrigin = null;
        selectedElement = null;
        panel.classList.remove('visible');
        outline.classList.remove('visible');
        badge.classList.remove('visible');
        hideInserts();
      } else {
        vscode.postMessage({ type: 'inspectMode', on: false });
      }
      return;
    }

    if (!selectedElement || !selectedElement.isConnected || !selectedOrigin) return;
    // Every origin in the tree names the text as it WAS until the next frame — see settle().
    if (settling) return;

    // Structural: the same messages the panel's own buttons send.
    if (event.key === 'Delete' || event.key === 'Backspace') {
      stop();
      vscode.postMessage({ type: 'arrange', origin: selectedOrigin });
      return;
    }

    if ((event.key === 'd' || event.key === 'D') && (event.metaKey || event.ctrlKey)) {
      stop();
      vscode.postMessage({ type: 'duplicate', origin: selectedOrigin });
      return;
    }

    if (event.altKey && (event.key === 'ArrowUp' || event.key === 'ArrowDown')) {
      stop();
      vscode.postMessage({ type: 'arrange', origin: selectedOrigin, delta: event.key === 'ArrowUp' ? -1 : 1 });
      return;
    }

    const walked = walk(event.key);
    if (walked) { stop(); select(walked); }
  });

  /** Where an arrow key goes in the tree — the tree the author wrote, not the DOM. */
  function walk(key) {
    if (key === 'ArrowUp') return stampedParent(selectedElement);
    if (key === 'ArrowDown') return stampedChildren(selectedElement)[0] || null;
    if (key !== 'ArrowLeft' && key !== 'ArrowRight') return null;

    const parent = stampedParent(selectedElement);
    if (!parent) return null;

    const siblings = stampedChildren(parent);
    const index = siblings.indexOf(selectedElement);
    if (index < 0) return null;
    return siblings[index + (key === 'ArrowLeft' ? -1 : 1)] || null;
  }

  // ---- what the host knows about a node, cached for as long as the tree is the same ---------------
  //
  // Keyed by ORIGIN, which is a span in the text, so one recompile invalidates the lot. Asked at most
  // once per node: the pointer crossing a screen would otherwise be a storm of round trips for
  // answers nobody reads. A pending question is remembered as null, an answered nothing as false.
  const known = new Map();
  let hovered = null;
  let hoverTimer = 0;
  /** True between an applied edit and the frame that reflects it — see the host's settle(). */
  let settling = false;

  function knownOf(element) {
    const origin = element && element.getAttribute && element.getAttribute('data-eq-origin');
    return origin ? known.get(origin) : undefined;
  }

  function ask(element) {
    const origin = element && element.getAttribute('data-eq-origin');
    if (!origin || known.has(origin)) return;
    known.set(origin, null);
    vscode.postMessage({ type: 'ask', origin: origin });
  }

  function forget() {
    known.clear();
    settling = false;
    hovered = null;
    hideInserts();
    caret.classList.remove('visible');
    drag = null;
    document.body.classList.remove('eq-dragging');
  }

  /** Hovering settles before it asks: a pointer travelling across the screen crosses dozens of nodes
   * and means none of them. */
  function hovering(element, event) {
    hovered = element;
    at = { clientX: event.clientX, clientY: event.clientY };
    // Not while the tree is stale: every answer would be about whatever now sits at those coordinates.
    if (settling) return;
    drawInserts(element);
    if (hoverTimer) clearTimeout(hoverTimer);
    hoverTimer = setTimeout(() => {
      ask(element);
      ask(stampedParent(element));
    }, 120);
  }

  // ---- inserting from the canvas -----------------------------------------------------------------
  let insertTarget = null;
  /** The last place the pointer was, which is what decides WHICH gap of a container is marked. */
  let at = { clientX: 0, clientY: 0 };

  function hideInserts() {
    plusBefore.classList.remove('visible');
    plusAfter.classList.remove('visible');
    gapMark.classList.remove('visible');
    insertTarget = null;
  }

  /**
   * Which way a container lays its children out, read off the RESULT rather than the CSS: a Row is
   * flex, a Grid is grid and a Stack is absolute, and all three answer this the same way once they
   * are on screen.
   */
  function axisOf(container, children) {
    if (children.length >= 2) {
      const first = children[0].getBoundingClientRect();
      const second = children[1].getBoundingClientRect();
      return (second.left >= first.right - 1 && second.top < first.bottom) ? 'x' : 'y';
    }

    // One box says nothing about direction, and defaulting to 'y' reads a Row's drop position off
    // the pointer's Y — a coordinate that means nothing there. With no result to read, ask the layout
    // itself. This is the one place the CSS is consulted, because it is the only thing that knows.
    const style = getComputedStyle(container);
    if (style.display.indexOf('flex') >= 0) return style.flexDirection.indexOf('row') === 0 ? 'x' : 'y';
    return style.display.indexOf('grid') >= 0 ? 'x' : 'y';
  }

  /**
   * The list a node sits in, when the canvas may act on it — and nothing at all when it may not.
   * <para>
   * The screen and the C# list have to line up child for child, or an index computed from pixels
   * names a different element in the file. A child that renders as several elements, or as none,
   * breaks that correspondence, so the affordance is WITHHELD rather than guessed at.
   * </para>
   */
  function placeIn(element) {
    const node = knownOf(element);
    if (!node || node.siblingIndex < 0) return null;

    const container = stampedParent(element);
    const list = knownOf(container);
    if (!list || list.childCount < 0) return null;

    const children = stampedChildren(container);
    if (children.length !== list.childCount) return null;

    return {
      container: container, children: children, index: node.siblingIndex,
      axis: axisOf(container, children),
    };
  }

  /**
   * What the pointer is offered where it is.
   *
   * Over a CHILD of a list: a + at each of its ends, which is "before this one" and "after this one" —
   * the two insertions anyone hovering a row actually means.
   *
   * Over a container's own space, including an empty one: the gap nearest the pointer is marked with
   * a dashed line and a single +. That mark is the answer to "where could something go", which is a
   * question the canvas should not make anyone guess at.
   */
  function drawInserts(element) {
    hideInserts();
    if (!inspecting || drag || settling) return;

    const place = placeIn(element);
    if (place) {
      const box = element.getBoundingClientRect();
      const middle = box.top + box.height / 2;
      if (place.axis === 'x' && !place.layered) {
        chipAt(plusBefore, box.left, middle);
        chipAt(plusAfter, box.right, middle);
      } else {
        chipAt(plusBefore, box.left + 10, box.top);
        chipAt(plusAfter, box.left + 10, box.bottom);
      }
      // Same two insertions either way — it is what they MEAN that differs, and a tooltip saying
      // "insert before" over a pile would be describing a row that is not there.
      plusBefore.title = place.layered ? 'Insert behind this' : 'Insert before this';
      plusAfter.title = place.layered ? 'Insert in front of this' : 'Insert after this';
      insertTarget = {
        origin: place.container.getAttribute('data-eq-origin'),
        before: place.index, after: place.index + 1,
      };
      return;
    }

    const here = listOf(element);
    if (!here) return;

    const slot = slotAt(at, here);
    if (here.layered) {
      // No gap to mark. The + goes where a new top layer would appear, and says so.
      const box = element.getBoundingClientRect();
      chipAt(plusBefore, box.left + box.width / 2, box.top + 12);
      plusBefore.title = 'Add on top';
      insertTarget = { origin: element.getAttribute('data-eq-origin'), before: here.children.length, after: null };
      return;
    }

    const rect = gapRect(here, slot);
    put(gapMark, rect);
    chipAt(plusBefore, rect.left + rect.width / 2, rect.top + rect.height / 2);
    plusBefore.title = 'Insert here';
    insertTarget = { origin: element.getAttribute('data-eq-origin'), before: slot, after: null };
  }

  /** The element's OWN list, when it has one this tool may write into. */
  function listOf(element) {
    const node = knownOf(element);
    if (!node || node.childCount < 0) return null;

    const children = stampedChildren(element);
    if (children.length !== node.childCount) return null;

    // A Stack's children overlap, so their order is DEPTH — the host says so, because geometry
    // cannot: two boxes on top of each other look exactly like two boxes nobody has laid out.
    const own = (node.lists || []).find((list) => list.name === 'children');
    return {
      container: element, children: children,
      layered: !!(own && own.layered),
      axis: axisOf(element, children),
    };
  }

  function chipAt(chip, x, y) {
    chip.style.left = (x - 9) + 'px';
    chip.style.top = (y - 9) + 'px';
    chip.classList.add('visible');
  }

  plusBefore.addEventListener('click', () => {
    if (insertTarget) vscode.postMessage({ type: 'insert', origin: insertTarget.origin, index: insertTarget.before });
  });
  plusAfter.addEventListener('click', () => {
    if (insertTarget && insertTarget.after !== null) {
      vscode.postMessage({ type: 'insert', origin: insertTarget.origin, index: insertTarget.after });
    }
  });

  // ---- dragging to reorder ------------------------------------------------------------------------
  let drag = null;

  /** Started only where the host has already said the node is an element of a list. A drag that could
   * not possibly land is a drag that should never begin. */
  function beginDrag(event, element) {
    if (!element || settling) return;
    const place = placeIn(element);
    if (!place) return;

    drag = {
      element: element,
      origin: element.getAttribute('data-eq-origin'),
      name: element.getAttribute('data-eq-component') || 'this',
      from: place.index, source: place.container,
      place: null, x: event.clientX, y: event.clientY, armed: false, slot: null,
    };
  }

  /**
   * The list under the pointer, when there is one this tool may write into.
   *
   * Recomputed on every move rather than fixed at the start, which is what lets a child leave the
   * container it was written in. The pointer is either over a child of the list or over the
   * container's own padding, so both are tried, nearest first.
   */
  function containerAt(target) {
    const found = stamped(target);
    if (!found) return null;

    for (const candidate of [found, stampedParent(found)]) {
      if (!candidate) continue;
      // Never asked. A drag lasts long enough for the answer to arrive and be used on a later move,
      // so this asks straight away rather than waiting for the pointer to settle.
      if (knownOf(candidate) === undefined) { ask(candidate); continue; }

      const found = listOf(candidate);
      if (found) return found;
    }
    return null;
  }

  function updateDrag(event) {
    if (!drag.armed) {
      // A threshold, so a click that trembles is still a click.
      if (Math.abs(event.clientX - drag.x) + Math.abs(event.clientY - drag.y) < 5) return;
      drag.armed = true;
      hideInserts();
      document.body.classList.add('eq-dragging');
    }

    const found = containerAt(event.target);
    // Into itself or into its own subtree there is no coherent answer, and the host says so too.
    drag.place = found && !drag.element.contains(found.container) ? found : null;
    drag.slot = drag.place ? slotAt(event, drag.place) : null;

    if (drag.slot === null) {
      caret.classList.remove('visible');
      say('no list here', event.clientX, event.clientY);
      return;
    }

    drawCaret(drag.slot, drag.place);

    if (drag.place.layered) {
      // Depth, not position: saying "3 of 5" about a pile would be counting something nobody sees.
      const under = drag.slot > 0 ? drag.place.children[drag.slot - 1] : null;
      say(drag.name + ' → ' + (under
        ? 'in front of ' + (under.getAttribute('data-eq-component') || 'that')
        : 'behind everything'), event.clientX, event.clientY);
      return;
    }

    // Leaving its own list, the node is added to the other one; staying, it is taken out first, so
    // everything after the gap it left shifts up by one.
    const home = drag.place.container === drag.source;
    const landing = home && drag.slot > drag.from ? drag.slot - 1 : drag.slot;
    const total = home ? drag.place.children.length : drag.place.children.length + 1;
    const where = home ? '' : 'into ' + (drag.place.container.getAttribute('data-eq-component') || 'list') + ' ';
    say(drag.name + ' → ' + where + (landing + 1) + ' of ' + total, event.clientX, event.clientY);
  }

  /** How many children the pointer has passed the middle of — which is the gap it is over. */
  function slotAt(event, place) {
    if (place.children.length === 0) return 0;

    // In a layer, "where" is "in front of which". The LAST child containing the pointer is the one
    // on top of the pile there, and dropping means landing just above it. Over the stack's own space
    // the answer is the top of the pile.
    if (place.layered) {
      for (let i = place.children.length - 1; i >= 0; i--) {
        const box = place.children[i].getBoundingClientRect();
        if (event.clientX >= box.left && event.clientX <= box.right
          && event.clientY >= box.top && event.clientY <= box.bottom) {
          return i + 1;
        }
      }
      return place.children.length;
    }


    // The NEAREST child, then which side of it. Projecting the pointer onto one axis instead counts
    // the children on every other line when a row wraps, or every other row of a grid, and lands the
    // node in a gap nobody pointed at.
    let best = 0;
    let nearest = Infinity;
    for (let i = 0; i < place.children.length; i++) {
      const box = place.children[i].getBoundingClientRect();
      const dx = Math.max(box.left - event.clientX, 0, event.clientX - box.right);
      const dy = Math.max(box.top - event.clientY, 0, event.clientY - box.bottom);
      const distance = dx * dx + dy * dy;
      if (distance < nearest) { nearest = distance; best = i; }
    }

    const box = place.children[best].getBoundingClientRect();
    const past = place.axis === 'x'
      ? event.clientX > box.left + box.width / 2
      : event.clientY > box.top + box.height / 2;
    return past ? best + 1 : best;
  }

  /**
   * The boundary between two children, as a rectangle to draw on.
   *
   * One geometry for both marks: the caret says where a dragged node WILL go, the dashed gap says
   * where a new one COULD. Two functions would drift, and the pair has to line up exactly.
   */
  function gapRect(place, slot) {
    const box = place.container.getBoundingClientRect();

    // Nothing to draw between: the mark takes the container's own middle, which is where its first
    // child would appear. Without this the branches below reach for a neighbour that does not exist.
    if (place.children.length === 0) {
      return { left: box.left, top: box.top + box.height / 2 - 1, width: box.width, height: 2, across: false };
    }

    const before = slot > 0 ? place.children[slot - 1].getBoundingClientRect() : null;
    const after = slot < place.children.length ? place.children[slot].getBoundingClientRect() : null;

    if (place.axis === 'x') {
      const x = before && after ? (before.right + after.left) / 2 : (after ? after.left : before.right);
      return { left: x - 1, top: box.top, width: 2, height: box.height, across: true };
    }

    const y = before && after ? (before.bottom + after.top) / 2 : (after ? after.top : before.bottom);
    return { left: box.left, top: y - 1, width: box.width, height: 2, across: false };
  }

  function put(mark, rect) {
    mark.style.left = rect.left + 'px';
    mark.style.top = rect.top + 'px';
    mark.style.width = rect.width + 'px';
    mark.style.height = rect.height + 'px';
    mark.classList.toggle('vertical', rect.across);
    mark.classList.add('visible');
  }

  function drawCaret(slot, place) {
    if (!place.layered) {
      caret.classList.remove('layer');
      put(caret, gapRect(place, slot));
      return;
    }

    // Between two overlapping children there is no gap to draw a line in. What can be shown is WHICH
    // one the node would come to rest in front of, so the mark covers that child instead.
    const under = slot > 0 ? place.children[slot - 1] : null;
    const box = (under || place.container).getBoundingClientRect();
    caret.classList.add('layer');
    put(caret, { left: box.left, top: box.top, width: box.width, height: box.height, across: false });
  }

  function say(text, x, y) {
    badge.textContent = text;
    badge.classList.add('visible');
    badge.style.left = (x + 12) + 'px';
    badge.style.top = (y + 14) + 'px';
  }

  /** True when a drag actually happened, which is what tells the click afterwards to stand down. */
  function endDrag() {
    const finished = drag;
    drag = null;
    caret.classList.remove('visible');
    document.body.classList.remove('eq-dragging');
    if (!finished || !finished.armed) return false;

    if (finished.slot === null || !finished.place) return true;

    // Both sides send the GAP the node was dropped into, which is what this side actually saw.
    // Turning that into a position is the host's arithmetic, where a test can reach it.
    if (finished.place.container !== finished.source) {
      vscode.postMessage({
        type: 'moveTo',
        origin: finished.origin,
        target: finished.place.container.getAttribute('data-eq-origin'),
        index: finished.slot,
      });
    } else if (finished.slot !== finished.from && finished.slot !== finished.from + 1) {
      // Both gaps beside a node mean "where it already is". The host refuses those with a sentence —
      // correctly, but a warning that says "you put it back" is noise, so it is not sent at all.
      vscode.postMessage({ type: 'reorder', origin: finished.origin, index: finished.slot });
    }
    return true;
  }

  /**
   * The whole rendered tree, as the author would recognise it, sent up for the sidebar.
   *
   * No round trip and no new question for the host: the DOM already carries an origin and a component
   * name on every node, which is exactly what a layers list is. It goes up on every render, because
   * that is when it changes.
   *
   * Bounded, and the bound is SAID rather than silently applied — a screen with a thousand rows would
   * otherwise post a thousand entries on every keystroke.
   */
  const TREE_LIMIT = 2000;

  function describeTree() {
    const nodes = [];
    let truncated = false;

    const walk = (element, parent, depth) => {
      for (const child of stampedChildren(element)) {
        if (nodes.length >= TREE_LIMIT) { truncated = true; return; }
        const origin = child.getAttribute('data-eq-origin');
        nodes.push({
          origin: origin,
          parent: parent,
          label: child.getAttribute('data-eq-component') || 'node',
          depth: depth,
        });
        walk(child, origin, depth + 1);
      }
    };

    walk(app, null, 0);
    vscode.postMessage({ type: 'tree', nodes: nodes, truncated: truncated });
  }

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
      why.textContent = '∅ ' + node.insertReason;
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

  const upButton = document.getElementById('panel-up');
  const downButton = document.getElementById('panel-down');
  const removeButton = document.getElementById('panel-remove');

  upButton.addEventListener('click', () =>
    vscode.postMessage({ type: 'arrange', origin: selectedOrigin, delta: -1 }));
  downButton.addEventListener('click', () =>
    vscode.postMessage({ type: 'arrange', origin: selectedOrigin, delta: 1 }));
  removeButton.addEventListener('click', () =>
    vscode.postMessage({ type: 'arrange', origin: selectedOrigin }));

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

  /**
   * One data list — a Grid's columns, a menu's items — with what is in it and a + to add.
   *
   * Entries are addressed by POSITION, not by origin: a GridTrack never renders, so nothing on the
   * canvas carries its span and there is nothing to click. That is also why they are here at all.
   */
  function buildList(list) {
    const row = document.createElement('div');
    row.className = 'eq-list';

    const name = document.createElement('span');
    name.className = 'lname';
    name.textContent = list.name;
    row.appendChild(name);

    const add = document.createElement('span');
    add.className = 'chip add';
    add.textContent = '+ ' + list.elementType;
    add.title = 'Add to ' + list.name;
    add.addEventListener('click', () => vscode.postMessage({
      type: 'insert', origin: selectedOrigin, index: list.count, list: list.name,
      elementType: list.elementType,
    }));
    row.appendChild(add);

    (list.entries || []).forEach((written, index) => {
      const entry = document.createElement('span');
      entry.className = 'entry';
      entry.appendChild(document.createTextNode(written));

      const remove = document.createElement('button');
      remove.type = 'button';
      remove.textContent = '×';
      remove.title = 'Remove ' + written;
      remove.addEventListener('click', () => vscode.postMessage({
        type: 'removeAt', origin: selectedOrigin, list: list.name, index: index,
      }));
      entry.appendChild(remove);
      row.appendChild(entry);
    });

    return row;
  }

  /**
   * A value's members, as a small sheet under the property that carries them.
   *
   * Key, type, value — one row each, the set ones first because they are what is true now, and a last
   * row whose key is a SELECT of everything not set yet. Choosing one and pressing + asks for its
   * value through the editor's own input, exactly as any other property does.
   */
  function memberRows(property) {
    const row = document.createElement('tr');
    row.className = 'members-row';
    const cell = document.createElement('td');
    cell.colSpan = 3;

    const sheet = document.createElement('table');
    sheet.className = 'members';

    const origin = selectedOrigin;
    const written = property.members.filter((member) => member.kind !== 'unset');

    for (const member of written) {
      const line = document.createElement('tr');

      const key = document.createElement('td');
      key.className = 'pname';
      key.textContent = member.name;
      if (member.summary) key.title = member.summary;

      const type = document.createElement('td');
      type.className = 'ptype';
      type.textContent = member.type;

      const value = document.createElement('td');
      value.className = 'pvalue';
      if (member.editable) {
        value.appendChild(editorFor(member, (next) => vscode.postMessage({
          type: 'edit', origin: origin, property: property.name + '.' + member.name, value: next,
        })));
      } else {
        value.textContent = member.reason || 'not reachable from this form';
        line.className = 'unreachable';
      }

      const drop = document.createElement('td');
      drop.className = 'pdrop';
      if (member.editable) {
        const remove = document.createElement('button');
        remove.type = 'button';
        remove.textContent = '×';
        remove.title = 'Unset ' + member.name;
        remove.addEventListener('click', () => vscode.postMessage({
          type: 'unset', origin: origin, property: property.name + '.' + member.name,
        }));
        drop.appendChild(remove);
      }

      line.append(key, type, value, drop);
      sheet.appendChild(line);
    }

    const rest = property.members.filter((member) => member.kind === 'unset' && member.editable);
    if (rest.length > 0) {
      const line = document.createElement('tr');
      line.className = 'add';

      const key = document.createElement('td');
      const select = document.createElement('select');
      for (const member of rest) {
        const option = document.createElement('option');
        option.value = member.name;
        option.textContent = member.name;
        option.title = member.summary || '';
        select.appendChild(option);
      }
      key.appendChild(select);

      const type = document.createElement('td');
      type.className = 'ptype';
      type.textContent = rest[0].type;
      select.addEventListener('change', () => {
        const chosen = rest.find((member) => member.name === select.value);
        type.textContent = chosen ? chosen.type : '';
      });

      const value = document.createElement('td');
      const add = document.createElement('button');
      add.type = 'button';
      add.className = 'chip add';
      add.textContent = '+ set';
      add.addEventListener('click', () => {
        const chosen = rest.find((member) => member.name === select.value);
        if (!chosen) return;
        vscode.postMessage({
          type: 'edit', origin: origin, property: property.name + '.' + chosen.name,
          options: chosen.options || undefined,
        });
      });
      value.appendChild(add);

      line.append(key, type, value, document.createElement('td'));
      sheet.appendChild(line);
    }

    cell.appendChild(sheet);
    row.appendChild(cell);
    return row;
  }

  const KEYS = 'Arrows walk the tree \u00B7 Alt+Arrows move \u00B7 Delete removes \u00B7 '
    + 'Cmd/Ctrl+D duplicates \u00B7 Esc lets go';

  function showSelection(payload) {
    panelTitle.textContent = payload.node ? payload.node.component : 'Selection';
    // Discoverable where someone would look for it: the keys are the panel's own gestures, and a
    // webview cannot advertise them in VS Code's keybinding tooltips.
    panelTitle.title = KEYS;
    panelTier.textContent = payload.tier + (payload.node ? ' · ' + payload.node.form : '');
    panelTier.title = payload.reason || '';
    // Arrangement is only a thing for a node that IS an element of a list. Everything else — a
    // container's only child, a node built by a helper — has no order to be moved within.
    const arrangeable = payload.node && payload.node.siblingIndex >= 0;
    for (const button of [upButton, downButton, removeButton]) button.hidden = !arrangeable;
    if (arrangeable) {
      upButton.disabled = payload.node.siblingIndex === 0;
      downButton.disabled = payload.node.siblingIndex === payload.node.siblingCount - 1;
      // In a layer the list order IS the paint order, so the same two gestures move the node through
      // depth rather than through position. The buttons do the same thing and stop saying the wrong
      // thing about it.
      const layered = payload.node.inLayer;
      upButton.title = layered ? 'Send backward' : 'Move up';
      downButton.title = layered ? 'Bring forward' : 'Move down';
      upButton.setAttribute('aria-label', upButton.title);
      downButton.setAttribute('aria-label', downButton.title);
    }

    panel.classList.remove('minimised');
    panel.classList.add('visible');

    panelBody.replaceChildren();
    panelBody.appendChild(buildTree(payload.node));

    for (const list of (payload.node && payload.node.lists) || []) {
      // Visual lists are the tree, and the tree is on the canvas — where you can see what you are
      // pointing at. Only the data ones need a place to live.
      if (list.visual) continue;
      panelBody.appendChild(buildList(list));
    }

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

      if (property.members) {
        // The value is a whole BoxStyle. Showing its C# here would put the one thing an author most
        // wants to change into the one cell they cannot change it in.
        const set = property.members.filter((member) => member.kind !== 'unset').length;
        value.className = 'pvalue count';
        value.textContent = set + ' of ' + property.members.length + ' set';
      } else if (!property.editable) {
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

      if (property.members) table.appendChild(memberRows(property));
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
      // Everything the canvas had learned was about spans in the PREVIOUS text. Keeping any of it
      // would draw a control for a child that has since moved, or gone.
      forget();
      void render(payload);
    } else if (payload.type === 'selected') {
      if (payload.tier !== 'literal') outline.classList.add(payload.tier);
      if (payload.node && payload.origin) known.set(payload.origin, payload.node);
      showSelection(payload);
    } else if (payload.type === 'known') {
      known.set(payload.origin, payload.node || false);
      // The pointer may still be on it: the answer arrived after the hover that asked for it, which
      // is the whole reason it was asked in advance.
      if (hovered) drawInserts(hovered);
    } else if (payload.type === 'reveal') {
      // The sidebar asked for a node. It is the same selection the canvas makes, so it goes through
      // the same door — the outline, the panel and the editor's cursor all follow.
      const found = app.querySelector('[data-eq-origin="' + payload.origin.replace(/"/g, '\\"') + '"]');
      if (found) select(found);
    } else if (payload.type === 'settling') {
      settling = true;
      hideInserts();
      caret.classList.remove('visible');
      drag = null;
      document.body.classList.remove('eq-dragging');
    } else if (payload.type === 'reselect') {
      // The node moved, so the origin the panel was holding now names its old neighbour.
      selectedOrigin = payload.origin;
    } else if (payload.type === 'deselect') {
      panel.classList.remove('visible');
      outline.classList.remove('visible');
      badge.classList.remove('visible');
      selectedElement = null;
      selectedOrigin = null;
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
