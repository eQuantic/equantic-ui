import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';
import { PreviewPanel } from './preview';
import { ProjectLayout, ProjectNotReady, resolveLayout } from './project';
import { Sidecar } from './sidecar';

let output: vscode.OutputChannel;
let diagnostics: vscode.DiagnosticCollection;

/** One design host per project, because the expensive thing it holds — the project's Roslyn
 * compilation — is per project and takes ~200 ms to build. */
const hosts = new Map<string, Sidecar>();
const panels = new Map<string, PreviewPanel>();

/** The preview the title-bar buttons act on. VS Code's own `activeWebviewPanelId` decides which
 * buttons are SHOWN; this is who they are sent to. */
let active: PreviewPanel | undefined;

export function activate(context: vscode.ExtensionContext): void {
  output = vscode.window.createOutputChannel('eQuantic UI');
  diagnostics = vscode.languages.createDiagnosticCollection('equanticUI');
  context.subscriptions.push(output, diagnostics);

  context.subscriptions.push(
    vscode.commands.registerCommand('equanticUI.openPreview', () => openPreview(context)),
    vscode.commands.registerCommand('equanticUI.startInspect', () => active?.setInspecting(true)),
    vscode.commands.registerCommand('equanticUI.stopInspect', () => active?.setInspecting(false)),
    vscode.commands.registerCommand('equanticUI.restartSidecar', () => restart()),
  );
}

function log(line: string): void {
  output.appendLine(`[${new Date().toISOString().slice(11, 19)}] ${line}`);
}

/**
 * The design host binary. Shipped inside the extension for a normal install; taken from the repo's
 * build output when someone is working ON the extension, which is the only reason the setting
 * exists.
 */
function resolveSidecar(context: vscode.ExtensionContext): string {
  const configured = vscode.workspace.getConfiguration('equanticUI').get<string>('sidecarPath', '');
  if (configured) return configured;

  const bundled = path.join(context.extensionPath, 'host', 'eqdesign.dll');
  if (fs.existsSync(bundled)) return bundled;

  // Developing the extension from the repo: src/eQuantic.UI.Design builds beside extensions/vscode.
  const fromRepo = path.join(
    context.extensionPath, '..', '..', 'src', 'eQuantic.UI.Design', 'bin', 'Debug', 'net10.0', 'eqdesign.dll');
  return path.resolve(fromRepo);
}

async function hostFor(context: vscode.ExtensionContext, layout: ProjectLayout): Promise<Sidecar> {
  const existing = hosts.get(layout.dir);
  if (existing) return existing;

  const dll = resolveSidecar(context);
  if (!fs.existsSync(dll)) {
    throw new ProjectNotReady(
      `The design host is missing at ${dll}. Build src/eQuantic.UI.Design, or set equanticUI.sidecarPath.`);
  }

  const sidecar = new Sidecar(dll, (line) => log(line));
  sidecar.start();

  const result = await sidecar.initialize(layout.dir, layout.refsFile, layout.generatedDir);
  log(`design host ready for ${result.assemblyName}: ${result.sourceFiles} source files, ${result.references} references, ${result.elapsedMs} ms`);

  hosts.set(layout.dir, sidecar);
  return sidecar;
}

async function openPreview(context: vscode.ExtensionContext): Promise<void> {
  const editor = vscode.window.activeTextEditor;
  if (!editor || editor.document.languageId !== 'csharp') {
    void vscode.window.showInformationMessage('Open a C# component or page first.');
    return;
  }

  const key = editor.document.uri.toString();
  const open = panels.get(key);
  if (open) {
    open.reveal();
    return;
  }

  try {
    const layout = resolveLayout(editor.document.uri.fsPath, vscode.workspace.getConfiguration('equanticUI').get<string>('runtimePath', ''));
    const sidecar = await hostFor(context, layout);

    const panel = new PreviewPanel(
      editor.document, layout, sidecar, diagnostics, log, context.extensionUri,
      () => {
        panels.delete(key);
        if (active === panel) active = undefined;
      },
      (focused) => { if (focused) active = panel; });
    panels.set(key, panel);
    active = panel;
    await panel.prime();
  } catch (error) {
    const message = (error as Error).message;
    log(`preview could not open: ${message}`);
    // A missing build step is the common case and it has a specific fix, so it is said plainly
    // rather than as "something went wrong".
    void vscode.window.showErrorMessage(`eQuantic UI: ${message}`);
  }
}

function restart(): void {
  for (const host of hosts.values()) host.dispose();
  hosts.clear();
  log('design hosts stopped — the next preview will start a fresh one');
}

export function deactivate(): void {
  for (const panel of panels.values()) panel.dispose();
  panels.clear();
  restart();
}
