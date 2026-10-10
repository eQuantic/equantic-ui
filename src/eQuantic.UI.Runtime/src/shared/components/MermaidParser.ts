import { $eq, MermaidEdge, MermaidEdgeRef, MermaidGraph, MermaidMessage, MermaidNode, MermaidNodeRef } from "../runtime-exports";

export class MermaidParser {
    static $slots: any = null;
    static $failure: any = null;

    static $init(): any {
        if (MermaidParser.$slots === null) {
            if (MermaidParser.$failure !== null) throw MermaidParser.$failure;
            let $slots: any = MermaidParser.$slots = {};
            try {
                $slots.skipWords = null;
                $slots.messageArrows = null;
                $slots.skipWords = ['subgraph', 'end', 'style', 'classDef', 'class', 'click', 'linkStyle', 'direction'];
                $slots.messageArrows = ['-->>', '->>', '-->', '->'];
            } catch ($error) {
                MermaidParser.$slots = null;
                throw MermaidParser.$failure = $eq.exceptions.typeInitialization('eQuantic.UI.Components.MermaidParser', $error);
            }
        }
        return MermaidParser.$slots;
    }

    static get skipWords(): string[] {
        return MermaidParser.$init().skipWords;
    }

    static set skipWords(value: string[]) {
        MermaidParser.$init().skipWords = value;
    }

    static get messageArrows(): string[] {
        return MermaidParser.$init().messageArrows;
    }

    static set messageArrows(value: string[]) {
        MermaidParser.$init().messageArrows = value;
    }

    static parse(source: string) {
        if (!source) return null;
        let lines = $eq.text.replace(source, '\r\n', '\n', 'ordinal').split('\n');
        let graph = MermaidParser.headerOf(lines);
        if (graph == null) return null;
        for (let i = 0; i < lines.length; i++) {
            let line = $eq.text.trim(MermaidParser.stripComment(lines[i]));
            if (line.length === 0) continue;
            if (MermaidParser.isHeader(line)) continue;
            if (graph.kind === 'sequence') MermaidParser.parseSequenceLine(graph, line); else MermaidParser.parseFlowchartLine(graph, line);
        }
        if (graph.kind === 'sequence') return graph.messages.length === 0 && graph.nodes.length === 0 ? null : graph;
        return graph.nodes.length === 0 ? null : graph;
    }

    static headerOf(lines: string[]) {
        for (let i = 0; i < lines.length; i++) {
            let line = $eq.text.trim(MermaidParser.stripComment(lines[i]));
            if (line.length === 0) continue;
            if ($eq.text.startsWith(line, 'sequenceDiagram', 'ordinal')) {
                let $n13: any; 
                return ($n13 = new MermaidGraph(), $n13.kind = 'sequence', $n13);
            }
            let rest = '';
            if ($eq.text.startsWith(line, 'flowchart', 'ordinal')) rest = $eq.text.trim(line.slice(9)); else if ($eq.text.startsWith(line, 'graph', 'ordinal')) rest = $eq.text.trim(line.slice(5)); else return null;
            if (rest === 'TD' || rest === 'TB') {
                let $n14: any; 
                return ($n14 = new MermaidGraph(), $n14.vertical = true, $n14);
            }
            if (rest === 'LR') {
                let $n15: any; 
                return ($n15 = new MermaidGraph(), $n15.vertical = false, $n15);
            }
            return null;
        }
        return null;
    }

    static isHeader(line: string) {
        return $eq.text.startsWith(line, 'sequenceDiagram', 'ordinal') || $eq.text.startsWith(line, 'flowchart', 'ordinal') || $eq.text.startsWith(line, 'graph', 'ordinal');
    }

    static stripComment(line: string) {
        let at = $eq.text.indexOf(line, '%%', 'ordinal');
        return at < 0 ? line : line.slice(0, at);
    }

    static parseFlowchartLine(graph: MermaidGraph, line: string) {
        for (const skip of MermaidParser.skipWords) if (line === skip || $eq.text.startsWith(line, skip + ' ', 'ordinal')) return;
        let text = $eq.text.endsWith(line, ';', 'ordinal') ? line.slice(0, (line.length - 1)) : line;
        let from = MermaidParser.nodeRefAt(text, 0);
        if (from == null) return;
        MermaidParser.declare(graph, from);
        let pos = from.end;
        let current = from.id;
        while (true) {
            pos = MermaidParser.skipSpaces(text, pos);
            if (pos >= text.length) break;
            let edge = MermaidParser.edgeRefAt(text, pos);
            if (edge == null) break;
            pos = MermaidParser.skipSpaces(text, edge.end);
            let to = MermaidParser.nodeRefAt(text, pos);
            if (to == null) break;
            MermaidParser.declare(graph, to);
            let $n16: any; 
            graph.edges.push(($n16 = new MermaidEdge(), $n16.from = current, $n16.to = to.id, $n16.label = edge.label, $n16.arrow = edge.arrow, $n16));
            current = to.id;
            pos = to.end;
        }
    }

    static declare(graph: MermaidGraph, nodeRef: MermaidNodeRef) {
        for (const known of graph.nodes) {
            if (known.id !== nodeRef.id) continue;
            if (nodeRef.shaped) {
                known.label = nodeRef.label;
                known.shape = nodeRef.shape;
            }
            return;
        }
        let $n17: any; 
        graph.nodes.push(($n17 = new MermaidNode(), $n17.id = nodeRef.id, $n17.label = nodeRef.label.length === 0 ? nodeRef.id : nodeRef.label, $n17.shape = nodeRef.shape, $n17));
    }

    static skipSpaces(text: string, pos: number) {
        while (pos < text.length && text[pos] === ' ') pos++;
        return pos;
    }

    static isIdChar(c: string) {
        return (/^\p{L}$/u.test(c)) || (/^\p{Nd}$/u.test(c)) || c === '_';
    }

    static nodeRefAt(text: string, pos: number) {
        let start = MermaidParser.skipSpaces(text, pos);
        let end = start;
        while (end < text.length && MermaidParser.isIdChar(text[end])) end++;
        if (end === start) return null;
        let $n18: any; 
        let node = ($n18 = new MermaidNodeRef(), $n18.id = text.slice(start, end), $n18.end = end, $n18);
        if (end >= text.length) return node;
        let c = text[end];
        if (c === '[') return MermaidParser.closeShape(text, end + 1, ']', 'rect', node);
        if (c === '{') return MermaidParser.closeShape(text, end + 1, '}', 'diamond', node);
        if (c === '(') {
            if (end + 1 < text.length && text[end + 1] === '(') return MermaidParser.closeShape(text, end + 2, '))', 'circle', node);
            return MermaidParser.closeShape(text, end + 1, ')', 'rounded', node);
        }
        return node;
    }

    static closeShape(text: string, from: number, closer: string, shape: string, node: MermaidNodeRef) {
        let close = $eq.text.indexOf(text, closer, from, 'ordinal');
        if (close < 0) return null;
        let label = $eq.text.trim(text.slice(from, close));
        if ($eq.text.startsWith(label, '"', 'ordinal') && $eq.text.endsWith(label, '"', 'ordinal') && label.length >= 2) label = label.slice(1, (label.length - 1));
        node.label = label;
        node.shape = shape;
        node.shaped = true;
        node.end = close + closer.length;
        return node;
    }

    static edgeRefAt(text: string, pos: number) {
        let i = pos;
        let body = 0;
        while (i < text.length && (text[i] === '-' || text[i] === '=' || text[i] === '.')) {
            body++;
            i++;
        }
        if (body < 2) return null;
        let arrow = i < text.length && text[i] === '>';
        if (arrow) i++;
        let $n19: any; 
        let edge = ($n19 = new MermaidEdgeRef(), $n19.arrow = arrow, $n19.end = i, $n19);
        let after = MermaidParser.skipSpaces(text, i);
        if (after < text.length && text[after] === '|') {
            let close = $eq.text.indexOfChar(text, '|', after + 1);
            if (close > after) {
                edge.label = $eq.text.trim(text.slice((after + 1), close));
                edge.end = close + 1;
            }
        }
        return edge;
    }

    static parseSequenceLine(graph: MermaidGraph, line: string) {
        if ($eq.text.startsWith(line, 'participant ', 'ordinal') || $eq.text.startsWith(line, 'actor ', 'ordinal')) {
            let rest = $eq.text.trim(line.slice((line.indexOf(' ') + 1)));
            let alias = rest;
            let display = rest;
            let asAt = $eq.text.indexOf(rest, ' as ', 'ordinal');
            if (asAt > 0) {
                alias = $eq.text.trim(rest.slice(0, asAt));
                display = $eq.text.trim(rest.slice((asAt + 4)));
            }
            MermaidParser.declareParticipant(graph, alias, display);
            return;
        }
        let colon = line.indexOf(':');
        if (colon <= 0) return;
        let head = $eq.text.trim(line.slice(0, colon));
        let label = $eq.text.trim(line.slice((colon + 1)));
        for (const arrow of MermaidParser.messageArrows) {
            let at = $eq.text.indexOf(head, arrow, 'ordinal');
            if (at <= 0) continue;
            let from = $eq.text.trim(head.slice(0, at));
            let to = $eq.text.trim(head.slice((at + arrow.length)));
            if (from.length === 0 || to.length === 0) return;
            MermaidParser.declareParticipant(graph, from, from);
            MermaidParser.declareParticipant(graph, to, to);
            let $n20: any; 
            graph.messages.push(($n20 = new MermaidMessage(), $n20.from = from, $n20.to = to, $n20.label = label, $n20.dashed = $eq.text.startsWith(arrow, '--', 'ordinal'), $n20));
            return;
        }
    }

    static declareParticipant(graph: MermaidGraph, id: string, display: string) {
        for (const known of graph.nodes) if (known.id === id) return;
        let $n21: any; 
        graph.nodes.push(($n21 = new MermaidNode(), $n21.id = id, $n21.label = display, $n21.shape = 'rect', $n21));
    }
}

