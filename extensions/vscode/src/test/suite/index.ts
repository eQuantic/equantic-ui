import * as assert from 'assert';
import * as path from 'path';
import * as vscode from 'vscode';

/**
 * The extension, loaded and driven inside a real VS Code.
 *
 * Every defect in this extension so far has been in the seam with the editor — a message posted
 * before the webview was listening, a template literal eating its escapes, a callback touching a
 * `const` the constructor it ran inside had not finished assigning. None of them can fail a unit
 * test, because none of them lives on the side a unit test can reach. They reached the user instead.
 *
 * No test framework: `@vscode/test-electron` asks for a module exporting `run()` that rejects on
 * failure, and `node:assert` is enough for that. One dev dependency, and it is the editor's own.
 */

type Test = { name: string; body: () => Promise<void> };

const tests: Test[] = [];
const test = (name: string, body: () => Promise<void>) => tests.push({ name, body });

/** The webview tabs VS Code currently shows for our preview. */
function previewTabs(): vscode.Tab[] {
  return vscode.window.tabGroups.all
    .flatMap((group) => group.tabs)
    .filter((tab) => tab.input instanceof vscode.TabInputWebview
      && (tab.input as vscode.TabInputWebview).viewType.includes('equanticUI.preview'));
}

async function closeAllPreviews(): Promise<void> {
  const tabs = previewTabs();
  if (tabs.length > 0) await vscode.window.tabGroups.close(tabs, false);
}

const sample = () => vscode.workspace.workspaceFolders?.[0]?.uri.fsPath ?? '';

test('the extension activates', async () => {
  const extension = vscode.extensions.getExtension('equantictech.equantic-ui');
  assert.ok(extension, 'the extension is not installed in this host');
  await extension.activate();
  assert.ok(extension.isActive, 'activate() did not complete');
});

test('every contributed command is registered', async () => {
  // The manifest and the code have to agree. A command declared and never registered shows up as
  // "command not found" the first time someone reaches for the button that runs it.
  const registered = await vscode.commands.getCommands(true);
  for (const command of [
    'equanticUI.openPreview',
    'equanticUI.startInspect',
    'equanticUI.stopInspect',
    'equanticUI.restartSidecar',
  ]) {
    assert.ok(registered.includes(command), `${command} is in the manifest but nothing registered it`);
  }
});

test('opening a preview brings up a panel and leaves no error behind', async () => {
  const screen = path.join(sample(), 'Screens', 'DeclarativeScreen.cs');
  const document = await vscode.workspace.openTextDocument(screen);
  await vscode.window.showTextDocument(document);

  // This resolving is most of the assertion. openPreview awaits the design host's initialize — so a
  // panel existing afterwards means the host started, read the reference list and built the project
  // compilation — and then awaits the first render, which waits on the webview announcing itself.
  // A broken handshake does not fail here, it HANGS, which the harness reports as a timeout.
  await vscode.commands.executeCommand('equanticUI.openPreview');

  assert.strictEqual(previewTabs().length, 1, 'no preview panel is open — see the eQuantic UI output channel');
});

test('opening the same file twice reveals the panel rather than opening a second', async () => {
  await vscode.commands.executeCommand('equanticUI.openPreview');
  assert.strictEqual(previewTabs().length, 1, 'a second panel opened for the same document');
});

test('inspect can be turned on and off from the commands the title bar runs', async () => {
  // The buttons are `when`-gated in the manifest and cannot be clicked from here, but the commands
  // behind them can — which is what catches a command that throws the moment it is invoked.
  await vscode.commands.executeCommand('equanticUI.startInspect');
  await vscode.commands.executeCommand('equanticUI.stopInspect');
});

test('a file outside any project refuses with a message rather than a panel', async () => {
  await closeAllPreviews();

  const stray = await vscode.workspace.openTextDocument({ language: 'csharp', content: 'class Stray { }' });
  await vscode.window.showTextDocument(stray);
  await vscode.commands.executeCommand('equanticUI.openPreview');

  assert.strictEqual(previewTabs().length, 0, 'a preview opened for a file that belongs to no project');
});

/**
 * A test that HANGS is the shape two of the bugs this suite exists for would take: a webview that
 * never announces itself leaves the first render awaiting forever, and so does a script that failed
 * to parse. Without a bound, that is a suite which never reports rather than one that fails.
 */
function within(milliseconds: number, body: () => Promise<void>): Promise<void> {
  return Promise.race([
    body(),
    new Promise<void>((_, reject) =>
      setTimeout(() => reject(new Error(`timed out after ${milliseconds} ms — it did not fail, it never answered`)),
        milliseconds).unref()),
  ]);
}

export async function run(): Promise<void> {
  const failures: string[] = [];

  for (const { name, body } of tests) {
    try {
      await within(30_000, body);
      console.log(`  ok  ${name}`);
    } catch (error) {
      failures.push(`${name}\n      ${(error as Error).message}`);
      console.log(`  FAIL ${name}`);
    }
  }

  await closeAllPreviews();

  console.log(`\n${tests.length - failures.length}/${tests.length} passed`);
  if (failures.length > 0) throw new Error(`\n  ${failures.join('\n  ')}`);
}
