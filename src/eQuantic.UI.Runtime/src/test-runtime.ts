/**
 * The runtime as the served bundle loads it, before every spec file (`setupFiles`): the export barrel
 * and every module it exports, which is what registers the element engine the lowering builds
 * through. A spec that lowers a tree without importing a component would otherwise reach the
 * lowering with no engine loaded, which a page in a browser never does (element-engine.ts says why
 * the lowering cannot import it itself). Nothing the runtime ships imports this file.
 */
import './shared/runtime-exports';
import { afterAll } from 'vitest';
import { releaseDetachedTableViewports } from './shared/table-viewports';

// A spec file's page leaves when the file ends, as a page does in a browser, and the tables it left take
// with them what they armed: a pan's click guard and its 400 ms timer, which otherwise fired after the
// file's window was torn down (#1876). A spec that pans a table needs no teardown of its own for it.
afterAll(() => {
  document.body.innerHTML = '';
  releaseDetachedTableViewports();
});
