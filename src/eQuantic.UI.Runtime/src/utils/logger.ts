/**
 * Professional logging system for eQuantic.UI
 * Only logs in development mode, silent in production
 */

const isDev = typeof window !== 'undefined' && window.__EQ_DEV__;

export const logger = {
  debug(...args: any[]) {
    if (isDev) console.debug('[eQuantic.UI]', ...args);
  },

  info(...args: any[]) {
    if (isDev) console.info('[eQuantic.UI]', ...args);
  },

  warn(...args: any[]) {
    console.warn('[eQuantic.UI]', ...args);
  },

  error(...args: any[]) {
    console.error('[eQuantic.UI]', ...args);
  },
};
