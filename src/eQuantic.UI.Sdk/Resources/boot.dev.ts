// eQuantic.UI Client Runtime, for development: the runtime every page imports, with the dev loop.
// The server serves this bundle (runtime.dev.js) in Development and under hot reload, and runtime.js,
// whose entry is boot.ts alone, everywhere else, so a production page loads no byte of src/dev (#1359).

export * from './boot';

import { installDevLoop } from './boot';
import { errorOverlay, installErrorOverlay } from '../../eQuantic.UI.Runtime/src/dev/error-overlay';
import {
  capturePageState,
  replayPageState,
  restorePageState,
} from '../../eQuantic.UI.Runtime/src/dev/hot-reload-state';

installDevLoop({
  installErrorOverlay,
  showBuildErrors: (errors) => errorOverlay.showBuildErrors(errors),
  capturePageState,
  restorePageState,
  replayPageState,
});
