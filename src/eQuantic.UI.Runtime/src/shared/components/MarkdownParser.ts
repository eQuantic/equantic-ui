import { $eq, MarkdownBlock, MarkdownBulletMatch, MarkdownCell, MarkdownLinkMatch, MarkdownListItem, MarkdownRow, MarkdownRun } from "../runtime-exports";

export class MarkdownParser {
    static parse(source: string) {
        let blocks: MarkdownBlock[] = [];
        if (!source) return blocks;
        let lines = MarkdownParser.stripComments($eq.text.replace(source, '\r\n', '\n', 'ordinal').split('\n'));
        let paragraph = '';
        let usedIds: Set<string> = $eq.collections.hashSet();
        for (let i = 0; i < lines.length; i++) {
            let line = lines[i];
            let trimmed = $eq.text.trim(line);
            if ($eq.text.startsWith(trimmed, '```', 'ordinal')) {
                paragraph = MarkdownParser.flushParagraph(paragraph, blocks);
                let lang = $eq.text.trim(trimmed.slice(3));
                let body: string[] = [];
                i++;
                while (i < lines.length && !$eq.text.startsWith($eq.text.trimStart(lines[i]), '```', 'ordinal')) {
                    body.push(lines[i]);
                    i++;
                }
                let $n1: any; 
                blocks.push(($n1 = new MarkdownBlock(), $n1.kind = 'code', $n1.lang = lang.length === 0 ? 'text' : lang, $n1.raw = $eq.text.join('\n', body), $n1));
                continue;
            }
            if (trimmed.length === 0) {
                paragraph = MarkdownParser.flushParagraph(paragraph, blocks);
                continue;
            }
            if ($eq.text.startsWith(trimmed, '#', 'ordinal')) {
                let level = 0;
                while (level < trimmed.length && trimmed[level] === '#') level++;
                if (level <= 6 && level < trimmed.length && trimmed[level] === ' ') {
                    paragraph = MarkdownParser.flushParagraph(paragraph, blocks);
                    let text = $eq.text.trim(trimmed.slice(level));
                    let id = MarkdownParser.slug(text);
                    let unique = id;
                    let n = 1;
                    while (usedIds.has(unique)) {
                        n++;
                        unique = id + '-' + $eq.text.format(n, null, undefined, undefined, 'int32');
                    }
                    $eq.collections.setAdd(usedIds, unique);
                    let $n2: any; 
                    blocks.push(($n2 = new MarkdownBlock(), $n2.kind = 'heading', $n2.level = level > 4 ? 4 : level, $n2.text = MarkdownParser.strip(text), $n2.runs = MarkdownParser.inline(text), $n2.id = unique, $n2));
                    continue;
                }
            }
            if (trimmed === '---' || trimmed === '***' || trimmed === '___') {
                paragraph = MarkdownParser.flushParagraph(paragraph, blocks);
                let $n3: any; 
                blocks.push(($n3 = new MarkdownBlock(), $n3.kind = 'rule', $n3));
                continue;
            }
            if ($eq.text.startsWith(trimmed, '|', 'ordinal') && i + 1 < lines.length && MarkdownParser.isAlignmentRow(lines[i + 1])) {
                paragraph = MarkdownParser.flushParagraph(paragraph, blocks);
                let $n4: any; 
                let table = ($n4 = new MarkdownBlock(), $n4.kind = 'table', $n4);
                for (const cell of MarkdownParser.splitRow(trimmed)) {
                    let $n5: any; 
                    table.head.push(($n5 = new MarkdownCell(), $n5.runs = MarkdownParser.inline(cell), $n5));
                }
                i += 2;
                while (i < lines.length && $eq.text.startsWith($eq.text.trim(lines[i]), '|', 'ordinal')) {
                    let row = new MarkdownRow();
                    for (const cell of MarkdownParser.splitRow($eq.text.trim(lines[i]))) {
                        let $n6: any; 
                        row.cells.push(($n6 = new MarkdownCell(), $n6.runs = MarkdownParser.inline(cell), $n6));
                    }
                    table.rows.push(row);
                    i++;
                }
                i--;
                blocks.push(table);
                continue;
            }
            if ($eq.text.startsWith(trimmed, '>', 'ordinal')) {
                paragraph = MarkdownParser.flushParagraph(paragraph, blocks);
                let quoted = '';
                while (i < lines.length && $eq.text.startsWith($eq.text.trimStart(lines[i]), '>', 'ordinal')) {
                    let q = $eq.text.trimStart(lines[i]);
                    q = $eq.text.trim(q.slice(1));
                    quoted = quoted.length === 0 ? q : quoted + ' ' + q;
                    i++;
                }
                i--;
                let $n7: any; 
                blocks.push(($n7 = new MarkdownBlock(), $n7.kind = 'quote', $n7.runs = MarkdownParser.inline($eq.text.trim(quoted)), $n7));
                continue;
            }
            let bullet = MarkdownParser.bulletOf(line);
            if (bullet != null) {
                paragraph = MarkdownParser.flushParagraph(paragraph, blocks);
                let $n8: any; 
                let list = ($n8 = new MarkdownBlock(), $n8.kind = 'list', $n8);
                while (i < lines.length) {
                    let mark = MarkdownParser.bulletOf(lines[i]);
                    if (mark == null) {
                        if (list.items.length > 0 && $eq.text.startsWith(lines[i], '  ', 'ordinal') && $eq.text.trim(lines[i]).length > 0 && !$eq.text.startsWith($eq.text.trimStart(lines[i]), '```', 'ordinal')) {
                            let last = list.items[list.items.length - 1];
                            for (const run of MarkdownParser.inline(' ' + $eq.text.trim(lines[i]))) last.runs.push(run);
                            i++;
                            continue;
                        }
                        break;
                    }
                    let indent = lines[i].length - $eq.text.trimStart(lines[i]).length;
                    let $n9: any; 
                    list.items.push(($n9 = new MarkdownListItem(), $n9.runs = MarkdownParser.inline(mark.content), $n9.depth = indent >= 2 ? 1 : 0, $n9.marker = mark.marker, $n9));
                    i++;
                }
                i--;
                blocks.push(list);
                continue;
            }
            paragraph = paragraph.length === 0 ? trimmed : paragraph + ' ' + trimmed;
        }
        MarkdownParser.flushParagraph(paragraph, blocks);
        return blocks;
    }

    static slug(text: string) {
        let plain = MarkdownParser.strip(text).toLowerCase();
        let slug = '';
        for (let i = 0; i < plain.length; i++) {
            let ch = plain[i];
            if ((/^\p{L}$/u.test(ch)) || (/^\p{Nd}$/u.test(ch))) slug += ch; else if (slug.length > 0 && slug[slug.length - 1] !== '-') slug += '-';
        }
        while (slug.length > 0 && slug[slug.length - 1] === '-') slug = slug.slice(0, (slug.length - 1));
        return slug;
    }

    static flushParagraph(paragraph: string, blocks: MarkdownBlock[]) {
        let joined = $eq.text.trim(paragraph);
        if (joined.length === 0) return '';
        let $n10: any; 
        blocks.push(($n10 = new MarkdownBlock(), $n10.kind = 'paragraph', $n10.runs = MarkdownParser.inline(joined), $n10));
        return '';
    }

    static stripComments(lines: string[]) {
        let clean: string[] = [];
        let fenced = false;
        let open = false;
        for (let i = 0; i < lines.length; i++) {
            let line = lines[i];
            if ($eq.text.startsWith($eq.text.trimStart(line), '```', 'ordinal')) {
                fenced = !fenced;
                clean.push(line);
                continue;
            }
            if (fenced) {
                clean.push(line);
                continue;
            }
            let text = line;
            if (open) {
                let close = $eq.text.indexOf(text, '-->', 'ordinal');
                if (close < 0) {
                    clean.push('');
                    continue;
                }
                text = text.slice((close + 3));
                open = false;
            }
            while (true) {
                let start = $eq.text.indexOf(text, '<!--', 'ordinal');
                if (start < 0) break;
                let end = $eq.text.indexOf(text, '-->', start, 'ordinal');
                if (end < 0) {
                    text = text.slice(0, start);
                    open = true;
                    break;
                }
                text = text.slice(0, start) + text.slice((end + 3));
            }
            clean.push($eq.text.trimEnd(text));
        }
        return clean;
    }

    static isAlignmentRow(line: string) {
        let t = $eq.text.trim(line);
        if (!$eq.text.startsWith(t, '|', 'ordinal')) return false;
        let hasDash = false;
        for (let i = 0; i < t.length; i++) {
            let ch = t[i];
            if (ch === '-') hasDash = true; else if (ch !== '|' && ch !== ':' && ch !== ' ') return false;
        }
        return hasDash;
    }

    static splitRow(line: string) {
        let cells: string[] = [];
        let t = $eq.text.trim(line);
        if (t.length > 0 && t[0] === '|') t = t.slice(1);
        if (t.length > 0 && t[t.length - 1] === '|') t = t.slice(0, (t.length - 1));
        let cell = '';
        let inCode = false;
        for (let i = 0; i < t.length; i++) {
            let ch = t[i];
            if (ch === '`') inCode = !inCode;
            if (ch === '|' && !inCode) {
                cells.push($eq.text.trim(cell));
                cell = '';
                continue;
            }
            cell += ch;
        }
        cells.push($eq.text.trim(cell));
        return cells;
    }

    static bulletOf(line: string) {
        let t = $eq.text.trimStart(line);
        if ($eq.text.startsWith(t, '- ', 'ordinal') || $eq.text.startsWith(t, '* ', 'ordinal')) {
            let $n11: any; 
            return ($n11 = new MarkdownBulletMatch(), $n11.marker = '•', $n11.content = $eq.text.trim(t.slice(2)), $n11);
        }
        let digits = 0;
        while (digits < t.length && (/^\p{Nd}$/u.test(t[digits]))) digits++;
        if (digits > 0 && digits + 1 < t.length && t[digits] === '.' && t[digits + 1] === ' ') {
            let $n12: any; 
            return ($n12 = new MarkdownBulletMatch(), $n12.marker = t.slice(0, digits) + '.', $n12.content = $eq.text.trim(t.slice((digits + 2))), $n12);
        }
        return null;
    }

    static inline(text: string) {
        let runs: MarkdownRun[] = [];
        if (!text) return runs;
        let buffer = '';
        let i = 0;
        while (i < text.length) {
            let c = text[i];
            if (c === '`') {
                let end = $eq.text.indexOfChar(text, '`', i + 1);
                if (end > i) {
                    buffer = MarkdownParser.flushText(runs, buffer);
                    let $n13: any; 
                    runs.push(($n13 = new MarkdownRun(), $n13.text = text.slice((i + 1), end), $n13.code = true, $n13));
                    i = end + 1;
                    continue;
                }
            } else if (c === '!' && i + 1 < text.length && text[i + 1] === '[') {
                let image = MarkdownParser.matchLink(text, i + 1);
                if (image != null) {
                    buffer = MarkdownParser.flushText(runs, buffer);
                    MarkdownParser.addLinkRuns(runs, image.label.length === 0 ? image.href : image.label, image.href);
                    i = image.end;
                    continue;
                }
            } else if (c === '[') {
                let link = MarkdownParser.matchLink(text, i);
                if (link != null) {
                    buffer = MarkdownParser.flushText(runs, buffer);
                    MarkdownParser.addLinkRuns(runs, link.label, link.href);
                    i = link.end;
                    continue;
                }
            } else if (c === '*' && i + 1 < text.length && text[i + 1] === '*') {
                let end = $eq.text.indexOf(text, '**', i + 2, 'ordinal');
                if (end > i) {
                    buffer = MarkdownParser.flushText(runs, buffer);
                    for (const run of MarkdownParser.inline(text.slice((i + 2), end))) {
                        run.bold = true;
                        runs.push(run);
                    }
                    i = end + 2;
                    continue;
                }
            } else if (c === '*') {
                let end = -1;
                for (let scan = i + 1; scan < text.length; scan++) {
                    if (text[scan] !== '*') continue;
                    if (scan + 1 < text.length && text[scan + 1] === '*') {
                        scan++;
                        continue;
                    }
                    end = scan;
                    break;
                }
                if (end > i + 1) {
                    buffer = MarkdownParser.flushText(runs, buffer);
                    for (const run of MarkdownParser.inline(text.slice((i + 1), end))) {
                        run.italic = true;
                        runs.push(run);
                    }
                    i = end + 1;
                    continue;
                }
            }
            buffer += c;
            i++;
        }
        MarkdownParser.flushText(runs, buffer);
        return runs;
    }

    static strip(text: string) {
        let flat = '';
        for (const run of MarkdownParser.inline(text)) flat += run.text;
        return flat;
    }

    static flushText(runs: MarkdownRun[], buffer: string) {
        if (buffer.length > 0) {
            let $n14: any; 
            runs.push(($n14 = new MarkdownRun(), $n14.text = buffer, $n14));
        }
        return '';
    }

    static matchLink(text: string, open: number) {
        let close = $eq.text.indexOfChar(text, ']', open + 1);
        if (close <= open || close + 1 >= text.length || text[close + 1] !== '(') return null;
        let hrefEnd = $eq.text.indexOfChar(text, ')', close + 2);
        if (hrefEnd <= close) return null;
        let $n15: any; 
        return ($n15 = new MarkdownLinkMatch(), $n15.label = text.slice((open + 1), close), $n15.href = text.slice((close + 2), hrefEnd), $n15.end = hrefEnd + 1, $n15);
    }

    static addLinkRuns(runs: MarkdownRun[], label: string, href: string) {
        let inner = MarkdownParser.inline(label);
        if (inner.length === 0) {
            let $n16: any; 
            runs.push(($n16 = new MarkdownRun(), $n16.text = label, $n16.href = href, $n16));
            return;
        }
        for (const run of inner) {
            run.href = href;
            runs.push(run);
        }
    }
}

