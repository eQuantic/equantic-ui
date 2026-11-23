/**
 * The twin of the C# `DevelopmentNotices` (#1780): what a component tells the developer while the app
 * runs in development, such as a picker drawn again without the value it reported. The development
 * runtime (`boot.dev.ts`) sets the sink to the console; a production page sets none, and a notice is
 * dropped where it is said.
 */
export const DevelopmentNotices = {
  sink: null as ((notice: string) => void) | null,

  say(notice: string): void {
    if (this.sink && notice) this.sink(notice);
  },
};
