import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { downloadAndUnzipVSCode, runTests } from '@vscode/test-electron';

/**
 * Where the downloaded VS Code actually keeps its binary.
 *
 * The harness composes the path itself and still expects `Contents/MacOS/Electron`; VS Code has since
 * renamed that to `Code`, so the launch fails with ENOENT on a file the harness just finished
 * downloading. Rather than pinning an old VS Code to keep an old name true, the executable is looked
 * for where it is — and if it is where the harness thinks, nothing here does anything.
 */
function executable(downloaded: string): string {
  if (fs.existsSync(downloaded)) return downloaded;

  const directory = path.dirname(downloaded);
  if (!fs.existsSync(directory)) return downloaded;

  const found = fs.readdirSync(directory).find((entry) => /^(Code|Electron|code)/.test(entry));
  return found ? path.join(directory, found) : downloaded;
}

/**
 * Downloads a VS Code, launches it with this extension loaded on the dashboard sample, and runs the
 * suite inside it. The sample is the workspace on purpose: the preview stands on an ordinary build's
 * output, so a project that has actually been built is the only place the whole path can run.
 */
function userDataDir(): string {
  const root = process.platform === 'win32' ? os.tmpdir() : '/tmp';
  const directory = path.join(root, 'eq-vsct');
  fs.mkdirSync(directory, { recursive: true });
  return directory;
}

async function main(): Promise<void> {
  const extensionDevelopmentPath = path.resolve(__dirname, '../../');
  const extensionTestsPath = path.resolve(__dirname, './suite/index');
  const workspace = path.resolve(extensionDevelopmentPath, '../../samples/DefaultUIDashboard');

  await runTests({
    vscodeExecutablePath: executable(await downloadAndUnzipVSCode()),
    extensionDevelopmentPath,
    extensionTestsPath,
    launchArgs: [
      workspace,
      // A SHORT user-data-dir, and not for tidiness: VS Code opens a Unix domain socket inside it,
      // and those cap at 103 characters. Left where the harness puts it — under the extension, under
      // the repo — the path overflows and the editor dies at startup with EINVAL. macOS's own temp
      // directory is itself long enough to overflow, so /tmp is named directly on posix.
      `--user-data-dir=${userDataDir()}`,
      // Someone else's extensions are someone else's failures: the C# extension in particular would
      // otherwise start a language server and race this one for the same project.
      '--disable-extensions',
      '--disable-gpu',
    ],
  });
}

main().catch((error) => {
  console.error('integration tests failed:', error);
  process.exit(1);
});
