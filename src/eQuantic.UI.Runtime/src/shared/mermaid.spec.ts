/**
 * The mermaid LAYOUT parity proof, client half. The C# suite (MermaidTests.cs) solves two
 * canonical diagrams and pins every placed box, segment, arrowhead and label into
 * `__fixtures__/mermaid-layout.txt`; this spec solves the SAME sources through the transpiled
 * twin and renders the SAME dump. The layout keeps to integers and exact halves precisely so
 * these two compilations agree to the character — a diagram that laid out differently after
 * hydration would visibly jump.
 */

import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { MermaidParser } from './__transpiled__/MermaidParser';
import { MermaidLayout } from './__transpiled__/MermaidLayout';
import type { MermaidScene } from './__transpiled__/MermaidScene';

// Resolved from the package root (vitest's cwd) — import.meta.url is http-schemed under the
// happy-dom environment, so a URL-relative read has no file to open.
const fixture = readFileSync('src/shared/__fixtures__/mermaid-layout.txt', 'utf8');

const FLOWCHART = [
  'graph TD',
  '  A[Start] --> B{Valid?}',
  '  B -->|yes| C(Deploy)',
  '  B -->|no| D[Fix]',
  '  D --> A',
  '  C --> E((Done))',
].join('\n');

const SEQUENCE = [
  'sequenceDiagram',
  '  participant U as User',
  '  participant S as Server',
  '  U->>S: POST /login',
  '  S-->>U: 200 + token',
  '  S->>S: audit',
].join('\n');

const FLOWCHART_LR = [
  'graph LR',
  '  A[C# components] --> B{eqc}',
  '  B -->|static shell| C[SSR HTML]',
  '  B -->|dynamic logic| D(TypeScript)',
  '  C --> E{Embedded Bun}',
  '  D --> E',
  '  E --> F[wwwroot/_equantic]',
].join('\n');

function dump(scene: MermaidScene): string {
  const lines = [`size ${scene.width}x${scene.height}`];
  for (const n of scene.nodes)
    lines.push(`node ${n.node.id}(${n.node.shape}) ${n.x},${n.y} ${n.w}x${n.h}`);
  for (const s of scene.segments) lines.push(`seg ${s.x},${s.y} ${s.w}x${s.h}`);
  for (const a of scene.arrows) lines.push(`arrow ${a.x},${a.y} d${a.direction}`);
  for (const l of scene.labels) lines.push(`label "${l.text}" ${l.x},${l.y}`);
  return lines.join('\n');
}

function solveAndDump(source: string): string {
  const graph = MermaidParser.parse(source);
  expect(graph).not.toBeNull();
  return dump(MermaidLayout.solve(graph!));
}

describe('Mermaid twin layout (cross-pinned with MermaidTests.cs)', () => {
  it('places every box on the same numbers the C# layout pinned', () => {
    const actual =
      '== flowchart ==\n' + solveAndDump(FLOWCHART)
      + '\n== flowchart-lr ==\n' + solveAndDump(FLOWCHART_LR)
      + '\n== sequence ==\n' + solveAndDump(SEQUENCE) + '\n';
    expect(actual).toBe(fixture);
  });

  it('falls back to null on grammars outside the subset', () => {
    expect(MermaidParser.parse('gantt\n  title Plan')).toBeNull();
    expect(MermaidParser.parse('')).toBeNull();
  });
});
