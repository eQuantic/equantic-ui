import { $eq, MarkdownBlock, MarkdownBulletMatch, MarkdownCell, MarkdownLinkMatch, MarkdownListItem, MarkdownRow, MarkdownRun } from "../runtime-exports";
export class MarkdownParser {
    static parse(source: string) {
        let blocks: MarkdownBlock[] = [];
        if (!source) return blocks;
        let lines = MarkdownParser.stripComments(source.replaceAll('\r\n', '\n').split('\n'));
        let paragraph = '';
        let usedIds: Set<string> = new Set();
        for (let i = 0; i < lines.length; i++) {
            let line = lines[i];
            let trimmed = line.trim();
            if (trimmed.startsWith('```')) {
                paragraph = MarkdownParser.flushParagraph(paragraph, blocks);
                let lang = trimmed.slice(3).trim();
                let body: string[] = [];
                i++;
                while (i < lines.length && !lines[i].trimStart().startsWith('```')) {
                    body.push(lines[i]);
                    i++;
                }
                blocks.push(new MarkdownBlock({ kind: 'code', lang: lang.length === 0 ? 'text' : lang, raw: body.join('\n') }));
                continue;
            }
            if (trimmed.length === 0) {
                paragraph = MarkdownParser.flushParagraph(paragraph, blocks);
                continue;
            }
            if (trimmed.startsWith('#')) {
                let level = 0;
                while (level < trimmed.length && trimmed[level] === '#') level++;
                if (level <= 6 && level < trimmed.length && trimmed[level] === ' ') {
                    paragraph = MarkdownParser.flushParagraph(paragraph, blocks);
                    let text = trimmed.slice(level).trim();
                    let id = MarkdownParser.slug(text);
                    let unique = id;
                    let n = 1;
                    while (usedIds.has(unique)) {
                        n++;
                        unique = id + '-' + n;
                    }
                    $eq.collections.setAdd(usedIds, unique);
                    blocks.push(new MarkdownBlock({ kind: 'heading', level: level > 4 ? 4 : level, text: MarkdownParser.strip(text), runs: MarkdownParser.inline(text), id: unique }));
                    continue;
                }
            }
            if (trimmed === '---' || trimmed === '***' || trimmed === '___') {
                paragraph = MarkdownParser.flushParagraph(paragraph, blocks);
                blocks.push(new MarkdownBlock({ kind: 'rule' }));
                continue;
            }
            if (trimmed.startsWith('|') && i + 1 < lines.length && MarkdownParser.isAlignmentRow(lines[i + 1])) {
                paragraph = MarkdownParser.flushParagraph(paragraph, blocks);
                let table = new MarkdownBlock({ kind: 'table' });
                for (const cell of MarkdownParser.splitRow(trimmed)) table.head.push(new MarkdownCell({ runs: MarkdownParser.inline(cell) }));
                i += 2;
                while (i < lines.length && lines[i].trim().startsWith('|')) {
                    let row = new MarkdownRow();
                    for (const cell of MarkdownParser.splitRow(lines[i].trim())) row.cells.push(new MarkdownCell({ runs: MarkdownParser.inline(cell) }));
                    table.rows.push(row);
                    i++;
                }
                i--;
                blocks.push(table);
                continue;
            }
            if (trimmed.startsWith('>')) {
                paragraph = MarkdownParser.flushParagraph(paragraph, blocks);
                let quoted = '';
                while (i < lines.length && lines[i].trimStart().startsWith('>')) {
                    let q = lines[i].trimStart();
                    q = q.slice(1).trim();
                    quoted = quoted.length === 0 ? q : quoted + ' ' + q;
                    i++;
                }
                i--;
                blocks.push(new MarkdownBlock({ kind: 'quote', runs: MarkdownParser.inline(quoted.trim()) }));
                continue;
            }
            let bullet = MarkdownParser.bulletOf(line);
            if (bullet != null) {
                paragraph = MarkdownParser.flushParagraph(paragraph, blocks);
                let list = new MarkdownBlock({ kind: 'list' });
                while (i < lines.length) {
                    let mark = MarkdownParser.bulletOf(lines[i]);
                    if (mark == null) {
                        if (list.items.length > 0 && lines[i].startsWith('  ') && lines[i].trim().length > 0 && !lines[i].trimStart().startsWith('```')) {
                            let last = list.items[list.items.length - 1];
                            for (const run of MarkdownParser.inline(' ' + lines[i].trim())) last.runs.push(run);
                            i++;
                            continue;
                        }
                        break;
                    }
                    let indent = lines[i].length - lines[i].trimStart().length;
                    list.items.push(new MarkdownListItem({ runs: MarkdownParser.inline(mark.content), depth: indent >= 2 ? 1 : 0, marker: mark.marker }));
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
        let joined = paragraph.trim();
        if (joined.length === 0) return '';
        blocks.push(new MarkdownBlock({ kind: 'paragraph', runs: MarkdownParser.inline(joined) }));
        return '';
    }

    static stripComments(lines: string[]) {
        let clean: string[] = [];
        let fenced = false;
        let open = false;
        for (let i = 0; i < lines.length; i++) {
            let line = lines[i];
            if (line.trimStart().startsWith('```')) {
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
                let close = text.indexOf('-->');
                if (close < 0) {
                    clean.push('');
                    continue;
                }
                text = text.slice((close + 3));
                open = false;
            }
            while (true) {
                let start = text.indexOf('<!--');
                if (start < 0) break;
                let end = text.indexOf('-->', start);
                if (end < 0) {
                    text = text.slice(0, start);
                    open = true;
                    break;
                }
                text = text.slice(0, start) + text.slice((end + 3));
            }
            clean.push(text.trimEnd());
        }
        return clean;
    }

    static isAlignmentRow(line: string) {
        let t = line.trim();
        if (!t.startsWith('|')) return false;
        let hasDash = false;
        for (let i = 0; i < t.length; i++) {
            let ch = t[i];
            if (ch === '-') hasDash = true; else if (ch !== '|' && ch !== ':' && ch !== ' ') return false;
        }
        return hasDash;
    }

    static splitRow(line: string) {
        let cells: string[] = [];
        let t = line.trim();
        if (t.length > 0 && t[0] === '|') t = t.slice(1);
        if (t.length > 0 && t[t.length - 1] === '|') t = t.slice(0, (t.length - 1));
        let cell = '';
        let inCode = false;
        for (let i = 0; i < t.length; i++) {
            let ch = t[i];
            if (ch === '`') inCode = !inCode;
            if (ch === '|' && !inCode) {
                cells.push(cell.trim());
                cell = '';
                continue;
            }
            cell += ch;
        }
        cells.push(cell.trim());
        return cells;
    }

    static bulletOf(line: string) {
        let t = line.trimStart();
        if (t.startsWith('- ') || t.startsWith('* ')) return new MarkdownBulletMatch({ marker: '•', content: t.slice(2).trim() });
        let digits = 0;
        while (digits < t.length && (/^\p{Nd}$/u.test(t[digits]))) digits++;
        if (digits > 0 && digits + 1 < t.length && t[digits] === '.' && t[digits + 1] === ' ') return new MarkdownBulletMatch({ marker: t.slice(0, digits) + '.', content: t.slice((digits + 2)).trim() });
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
                let end = text.indexOf('`', i + 1);
                if (end > i) {
                    buffer = MarkdownParser.flushText(runs, buffer);
                    runs.push(new MarkdownRun({ text: text.slice((i + 1), end), code: true }));
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
                let end = text.indexOf('**', i + 2);
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
        if (buffer.length > 0) runs.push(new MarkdownRun({ text: buffer }));
        return '';
    }

    static matchLink(text: string, open: number) {
        let close = text.indexOf(']', open + 1);
        if (close <= open || close + 1 >= text.length || text[close + 1] !== '(') return null;
        let hrefEnd = text.indexOf(')', close + 2);
        if (hrefEnd <= close) return null;
        return new MarkdownLinkMatch({ label: text.slice((open + 1), close), href: text.slice((close + 2), hrefEnd), end: hrefEnd + 1 });
    }

    static addLinkRuns(runs: MarkdownRun[], label: string, href: string) {
        let inner = MarkdownParser.inline(label);
        if (inner.length === 0) {
            runs.push(new MarkdownRun({ text: label, href: href }));
            return;
        }
        for (const run of inner) {
            run.href = href;
            runs.push(run);
        }
    }
}

