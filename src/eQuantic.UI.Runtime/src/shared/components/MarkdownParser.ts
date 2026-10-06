import { $eq, MarkdownBlock, MarkdownBulletMatch, MarkdownCell, MarkdownLinkMatch, MarkdownListItem, MarkdownRow, MarkdownRun } from "../runtime-exports";

export class MarkdownParser {
    static parse(source: string) {
        let blocks: MarkdownBlock[] = [];
        if (!source) return blocks;
        let lines = MarkdownParser.stripComments($eq.text.replace(source, '\r\n', '\n', 'ordinal').split('\n'));
        let paragraph = '';
        let usedIds: Set<string> = new Set();
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
                blocks.push((($o: any, $3: any) => {
                    $o.raw = $3;
                    return $o;
                })((($o: any, $2: any) => {
                    $o.lang = $2;
                    return $o;
                })((($o: any, $1: any) => {
                    $o.kind = $1;
                    return $o;
                })(new MarkdownBlock(), 'code'), lang.length === 0 ? 'text' : lang), body.join('\n')));
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
                        unique = id + '-' + n;
                    }
                    $eq.collections.setAdd(usedIds, unique);
                    blocks.push((($o: any, $5: any) => {
                        $o.id = $5;
                        return $o;
                    })((($o: any, $4: any) => {
                        $o.runs = $4;
                        return $o;
                    })((($o: any, $3: any) => {
                        $o.text = $3;
                        return $o;
                    })((($o: any, $2: any) => {
                        $o.level = $2;
                        return $o;
                    })((($o: any, $1: any) => {
                        $o.kind = $1;
                        return $o;
                    })(new MarkdownBlock(), 'heading'), level > 4 ? 4 : level), MarkdownParser.strip(text)), MarkdownParser.inline(text)), unique));
                    continue;
                }
            }
            if (trimmed === '---' || trimmed === '***' || trimmed === '___') {
                paragraph = MarkdownParser.flushParagraph(paragraph, blocks);
                blocks.push((($o: any, $1: any) => {
                    $o.kind = $1;
                    return $o;
                })(new MarkdownBlock(), 'rule'));
                continue;
            }
            if ($eq.text.startsWith(trimmed, '|', 'ordinal') && i + 1 < lines.length && MarkdownParser.isAlignmentRow(lines[i + 1])) {
                paragraph = MarkdownParser.flushParagraph(paragraph, blocks);
                let table = (($o: any, $1: any) => {
                    $o.kind = $1;
                    return $o;
                })(new MarkdownBlock(), 'table');
                for (const cell of MarkdownParser.splitRow(trimmed)) table.head.push((($o: any, $1: any) => {
                    $o.runs = $1;
                    return $o;
                })(new MarkdownCell(), MarkdownParser.inline(cell)));
                i += 2;
                while (i < lines.length && $eq.text.startsWith($eq.text.trim(lines[i]), '|', 'ordinal')) {
                    let row = new MarkdownRow();
                    for (const cell of MarkdownParser.splitRow($eq.text.trim(lines[i]))) row.cells.push((($o: any, $1: any) => {
                        $o.runs = $1;
                        return $o;
                    })(new MarkdownCell(), MarkdownParser.inline(cell)));
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
                blocks.push((($o: any, $2: any) => {
                    $o.runs = $2;
                    return $o;
                })((($o: any, $1: any) => {
                    $o.kind = $1;
                    return $o;
                })(new MarkdownBlock(), 'quote'), MarkdownParser.inline($eq.text.trim(quoted))));
                continue;
            }
            let bullet = MarkdownParser.bulletOf(line);
            if (bullet != null) {
                paragraph = MarkdownParser.flushParagraph(paragraph, blocks);
                let list = (($o: any, $1: any) => {
                    $o.kind = $1;
                    return $o;
                })(new MarkdownBlock(), 'list');
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
                    list.items.push((($o: any, $3: any) => {
                        $o.marker = $3;
                        return $o;
                    })((($o: any, $2: any) => {
                        $o.depth = $2;
                        return $o;
                    })((($o: any, $1: any) => {
                        $o.runs = $1;
                        return $o;
                    })(new MarkdownListItem(), MarkdownParser.inline(mark.content)), indent >= 2 ? 1 : 0), mark.marker));
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
        blocks.push((($o: any, $2: any) => {
            $o.runs = $2;
            return $o;
        })((($o: any, $1: any) => {
            $o.kind = $1;
            return $o;
        })(new MarkdownBlock(), 'paragraph'), MarkdownParser.inline(joined)));
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
        if ($eq.text.startsWith(t, '- ', 'ordinal') || $eq.text.startsWith(t, '* ', 'ordinal')) return (($o: any, $2: any) => {
            $o.content = $2;
            return $o;
        })((($o: any, $1: any) => {
            $o.marker = $1;
            return $o;
        })(new MarkdownBulletMatch(), '•'), $eq.text.trim(t.slice(2)));
        let digits = 0;
        while (digits < t.length && (/^\p{Nd}$/u.test(t[digits]))) digits++;
        if (digits > 0 && digits + 1 < t.length && t[digits] === '.' && t[digits + 1] === ' ') return (($o: any, $2: any) => {
            $o.content = $2;
            return $o;
        })((($o: any, $1: any) => {
            $o.marker = $1;
            return $o;
        })(new MarkdownBulletMatch(), t.slice(0, digits) + '.'), $eq.text.trim(t.slice((digits + 2))));
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
                    runs.push((($o: any, $1: any, $2: any) => {
                        $o.text = $1;
                        $o.code = $2;
                        return $o;
                    })(new MarkdownRun(), text.slice((i + 1), end), true));
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
        if (buffer.length > 0) runs.push((($o: any, $1: any) => {
            $o.text = $1;
            return $o;
        })(new MarkdownRun(), buffer));
        return '';
    }

    static matchLink(text: string, open: number) {
        let close = text.indexOf(']', open + 1);
        if (close <= open || close + 1 >= text.length || text[close + 1] !== '(') return null;
        let hrefEnd = text.indexOf(')', close + 2);
        if (hrefEnd <= close) return null;
        return (($o: any, $3: any) => {
            $o.end = $3;
            return $o;
        })((($o: any, $2: any) => {
            $o.href = $2;
            return $o;
        })((($o: any, $1: any) => {
            $o.label = $1;
            return $o;
        })(new MarkdownLinkMatch(), text.slice((open + 1), close)), text.slice((close + 2), hrefEnd)), hrefEnd + 1);
    }

    static addLinkRuns(runs: MarkdownRun[], label: string, href: string) {
        let inner = MarkdownParser.inline(label);
        if (inner.length === 0) {
            runs.push((($o: any, $2: any) => {
                $o.href = $2;
                return $o;
            })((($o: any, $1: any) => {
                $o.text = $1;
                return $o;
            })(new MarkdownRun(), label), href));
            return;
        }
        for (const run of inner) {
            run.href = href;
            runs.push(run);
        }
    }
}

