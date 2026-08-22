/**
 * The markdown parser's PARITY proof, client half. The fixture document and every expected shape
 * literal here are cross-pinned with `MarkdownParserTests` on the C# side: one canonical document,
 * one shape dumper mirrored line for line, identical expected strings in both suites. If the twin
 * ever disagrees with the C# parser — SSR would render one tree and hydration another — one of
 * these two suites breaks before a page does.
 */

import { describe, expect, it } from 'vitest';
import { MarkdownParser } from './__transpiled__/MarkdownParser';
import type { MarkdownBlock } from './__transpiled__/MarkdownBlock';
import type { MarkdownRun } from './__transpiled__/MarkdownRun';

// Backticks live inside the document, so it is written as lines — the C# twin does the same.
const FIXTURE = [
  '# Título *forte*',
  '',
  'Intro paragraph with **bold**, *italic*, `code **not bold**`, a [link](https://eq.dev) and ![logo](https://eq.dev/logo.png).',
  '',
  '## Getting started',
  '',
  '- first item',
  '- second **item**',
  '  continues here',
  '  - nested',
  '1. one',
  '2. two',
  '',
  '> A quote with `pipes | inside`.',
  '',
  '```csharp',
  'var x = "# not a heading | not a table";',
  '```',
  '',
  '| Col A | Col B |',
  '| --- | --- |',
  '| `a | b` | plain |',
  '',
  '---',
  '',
  '## Getting started',
  '',
  'Done. <!-- a comment --> Kept.',
].join('\n');

const EXPECTED = [
  'h1(título-forte) Título forte',
  'p Intro paragraph with **bold**, *italic*, `code **not bold**`, a [link](https://eq.dev) and [logo](https://eq.dev/logo.png).',
  'h2(getting-started) Getting started',
  'list •@0 first item; •@0 second **item** continues here; •@1 nested; 1.@0 one; 2.@0 two',
  'q A quote with `pipes | inside`.',
  'code(csharp) var x = "# not a heading | not a table";',
  'table head=Col A~Col B row0=`a | b`~plain',
  'hr',
  'h2(getting-started-2) Getting started',
  'p Done.  Kept.',
];

function runText(run: MarkdownRun): string {
  let t = run.text;
  if (run.code) t = '`' + t + '`';
  if (run.italic) t = '*' + t + '*';
  if (run.bold) t = '**' + t + '**';
  if (run.href.length > 0) t = '[' + t + '](' + run.href + ')';
  return t;
}

const runsText = (runs: MarkdownRun[]): string => runs.map(runText).join('');

function shape(b: MarkdownBlock): string {
  switch (b.kind) {
    case 'heading':
      return `h${b.level}(${b.id}) ${b.text}`;
    case 'paragraph':
      return 'p ' + runsText(b.runs);
    case 'quote':
      return 'q ' + runsText(b.runs);
    case 'code':
      return `code(${b.lang}) ` + b.raw.replaceAll('\n', '⏎');
    case 'rule':
      return 'hr';
    case 'list':
      return (
        'list ' + b.items.map((it) => `${it.marker}@${it.depth} ${runsText(it.runs)}`).join('; ')
      );
    case 'table':
      return (
        'table head=' +
        b.head.map((c) => runsText(c.runs)).join('~') +
        b.rows.map((r, n) => ` row${n}=` + r.cells.map((c) => runsText(c.runs)).join('~')).join('')
      );
    default:
      return `?${b.kind}`;
  }
}

describe('MarkdownParser twin (cross-pinned with MarkdownParserTests.cs)', () => {
  it('parses the canonical document to the pinned shapes', () => {
    expect(MarkdownParser.parse(FIXTURE).map(shape)).toEqual(EXPECTED);
  });

  it('keeps markdown syntax literal inside a code span', () => {
    const runs = MarkdownParser.inline('before `**still two stars**` after');
    expect(runsText(runs)).toBe('before `**still two stars**` after');
    expect(runs[1].code).toBe(true);
    expect(runs[1].bold).toBe(false);
  });

  it('closes italic past a bold pair — *Since **x*** is italic wrapping bold', () => {
    const runs = MarkdownParser.inline('*Since **0.2.0***');
    expect(runs.map((r) => `${r.text}/${r.bold ? 'b' : ''}${r.italic ? 'i' : ''}`)).toEqual([
      'Since /i',
      '0.2.0/bi',
    ]);
  });
});
