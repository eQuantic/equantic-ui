/**
 * The runtime as the served bundle loads it, before every spec file (`setupFiles`): the export barrel
 * and every module it exports, which is what registers the element engine the lowering builds
 * through. A spec that lowers a tree without importing a component would otherwise reach the
 * lowering with no engine loaded, which a page in a browser never does (element-engine.ts says why
 * the lowering cannot import it itself). Nothing the runtime ships imports this file.
 */
import './shared/runtime-exports';
