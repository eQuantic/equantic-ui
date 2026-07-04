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
  },
});
