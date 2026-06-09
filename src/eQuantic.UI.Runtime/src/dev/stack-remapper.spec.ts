import { describe, it, expect } from 'vitest';
import { parseFrame, remapStackTrace } from './stack-remapper';
import { RawSourceMap } from './source-map';

describe('parseFrame', () => {
  it('parses a Chrome frame with a function name', () => {
    expect(
      parseFrame('    at Counter.increment (http://localhost:5000/_equantic/Counter.js:1:12)'),
    ).toEqual({
      func: 'Counter.increment',
      url: 'http://localhost:5000/_equantic/Counter.js',
      line: 1,
      column: 12,
    });
  });

  it('parses a Chrome frame without a function name', () => {
    expect(parseFrame('    at http://localhost/app.js:3:5')).toEqual({
      func: null,
      url: 'http://localhost/app.js',
      line: 3,
      column: 5,
    });
  });

  it('parses a Firefox frame', () => {
    expect(parseFrame('increment@http://localhost/Counter.js:1:11')).toEqual({
      func: 'increment',
      url: 'http://localhost/Counter.js',
      line: 1,
      column: 11,
    });
  });

  it('ignores non-frame lines', () => {
    expect(parseFrame('Error: something went wrong')).toBeNull();
  });
});

describe('remapStackTrace', () => {
  const map: RawSourceMap = {
    version: 3,
    sources: ['Counter.cs'],
    sourcesContent: ['l1\nl2\nl3\nl4\nl5\nl6\nl7'],
    names: ['Increment'],
    mappings: 'AAKQA,UACJA',
  };

  it('remaps JS frames back to C# using the fetched map', async () => {
    const stack = [
      'Error: boom',
      '    at increment (http://h/Counter.js:1:1)',
      '    at http://h/Counter.js:1:11',
    ].join('\n');

    const frames = await remapStackTrace(stack, async () => map);

    expect(frames).toHaveLength(2);
    expect(frames[0].mapped).toBe(true);
    expect(frames[0].file).toBe('Counter.cs');
    expect(frames[0].line).toBe(6); // col 1 -> 0-based 0 -> first segment
    expect(frames[1].file).toBe('Counter.cs');
    expect(frames[1].line).toBe(7); // col 11 -> 0-based 10 -> second segment
  });

  it('falls back to the JS frame when no map is available', async () => {
    const frames = await remapStackTrace('    at f (http://h/x.js:2:3)', async () => null);
    expect(frames[0].mapped).toBe(false);
    expect(frames[0].file).toBe('http://h/x.js');
    expect(frames[0].line).toBe(2);
  });

  it('fetches each map only once per URL', async () => {
    let calls = 0;
    const stack = '    at a (http://h/x.js:1:1)\n    at b (http://h/x.js:1:11)';
    await remapStackTrace(stack, async () => {
      calls++;
      return map;
    });
    expect(calls).toBe(1);
  });
});
