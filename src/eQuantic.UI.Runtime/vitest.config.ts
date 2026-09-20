import { resolve } from 'node:path';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  resolve: {
    alias: {
      // Transpiled fixtures import the runtime exactly like emitted app code does.
      '@equantic/runtime': resolve(__dirname, 'src/index.ts'),
    },
  },
  test: {
    environment: 'happy-dom',
    // happy-dom performs a REAL navigation for a link the router deliberately lets through, and a
    // unit suite has no business reaching the network: the request to `http://localhost:3000/counter`
    // was refused, the rejection arrived after the test that caused it had passed, and vitest failed
    // a run in which all 134 files were green. The tests that care about leaving the site assert it
    // through a mocked `location.assign`, so nothing here needs the navigation to be real.
    environmentOptions: {
      happyDOM: {
        settings: {
          navigation: { disableMainFrameNavigation: true, disableChildFrameNavigation: true },
        },
      },
    },
  },
});
