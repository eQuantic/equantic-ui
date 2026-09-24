import { $eq, CodePatchFile, CodePatchHunk, CodePatchLine } from "../runtime-exports";

export class CodePatch {
    static parse(text: string) {
        const close = () => {
            if (open && (hunks.length > 0 || binary || !(originalPath == null) || !(modifiedPath == null))) files.push(new CodePatchFile(originalPath, modifiedPath, hunks, binary));
            originalPath = null;
            modifiedPath = null;
            binary = false;
            hunks = [];
            open = false;
        };
        let lines = text.replaceAll('\r\n', '\n').split('\n');
        let files: CodePatchFile[] = [];
        let originalPath: string | null = null;
        let modifiedPath: string | null = null;
        let binary = false;
        let hunks: CodePatchHunk[] = [];
        let open = false;
        let i = 0;
        while (i < lines.length) {
            let line = lines[i];
            if (line.startsWith('diff --git ')) {
                close();
                open = true;
                let [a, b] = CodePatch.gitHeaderPaths(line.slice('diff --git '.length));
                originalPath = a;
                modifiedPath = b;
                i++;
                continue;
            }
            if (line.startsWith('--- ') && i + 1 < lines.length && lines[i + 1].startsWith('+++ ')) {
                if (!open || hunks.length > 0) close();
                open = true;
                originalPath = CodePatch.pathOf(line.slice(4));
                modifiedPath = CodePatch.pathOf(lines[i + 1].slice(4));
                i += 2;
                continue;
            }
            if (line.startsWith('rename from ')) originalPath = line.slice('rename from '.length); else if (line.startsWith('rename to ')) modifiedPath = line.slice('rename to '.length); else if (line.startsWith('Binary files ')) binary = true; else { let header: any; if (line.startsWith('@@ ') && (header = CodePatch.header(line)) != null) {
                open = true;
                i = CodePatch.readHunk(lines, i + 1, header, line, hunks);
                continue;
            } }
            i++;
        }
        close();
        return files;
    }

    static header(line: string): [number, number, number, number, string] | null {
        let close = line.indexOf(' @@', 3);
        if (close < 0) return null;
        let parts = line.slice(3, close).split(' ');
        if (parts.length !== 2 || !parts[0].startsWith('-') || !parts[1].startsWith('+')) return null;
        let original: any; let modified: any; 
        if (!((original = CodePatch.range(parts[0].slice(1))) != null) || !((modified = CodePatch.range(parts[1].slice(1))) != null)) return null;
        let section = close + 3 < line.length ? $eq.text.trimStart(line.slice((close + 3))) : '';
        return [original[0], original[1], modified[0], modified[1], section];
    }

    static range(text: string): [number, number] | null {
        let line: any, count: any;
        let comma = text.indexOf(',');
        let lineText = comma < 0 ? text : text.slice(0, comma);
        let countText = comma < 0 ? '1' : text.slice((comma + 1));
        if (!((line = $eq.num.intTryParse(lineText, 'int')) !== undefined || ((line = 0), false)) || !((count = $eq.num.intTryParse(countText, 'int')) !== undefined || ((count = 0), false))) return null;
        return [line, count];
    }

    static readHunk(lines: string[], at: number, header: [number, number, number, number, string], headerLine: string, hunks: CodePatchHunk[]) {
        let body: CodePatchLine[] = [];
        let original = 0;
        let modified = 0;
        let i = at;
        while (i < lines.length && (original < header[1] || modified < header[3])) {
            let line = lines[i];
            if (line.startsWith('\\')) {
                i++;
                continue;
            }
            let mark = line.length > 0 ? line[0] : ' ';
            let content = line.length > 0 ? line.slice(1) : '';
            if (mark === '-') {
                body.push(new CodePatchLine('removed', content));
                original++;
            } else if (mark === '+') {
                body.push(new CodePatchLine('added', content));
                modified++;
            } else if (mark === ' ') {
                body.push(new CodePatchLine('context', content));
                original++;
                modified++;
            } else break;
            i++;
        }
        while (i < lines.length && lines[i].startsWith('\\')) i++;
        hunks.push(new CodePatchHunk(header[1] === 0 ? header[0] : header[0] - 1, header[1], header[3] === 0 ? header[2] : header[2] - 1, header[3], header[4], body, headerLine));
        return i;
    }

    static pathOf(text: string) {
        let tab = text.indexOf('\t');
        let path = tab < 0 ? text : text.slice(0, tab);
        if (path === '/dev/null') return null;
        if (path.startsWith('a/') || path.startsWith('b/')) return path.slice(2);
        return path;
    }

    static gitHeaderPaths(text: string): [string | null, string | null] {
        let split = text.lastIndexOf(' b/');
        if (split < 0) return [null, null];
        return [CodePatch.pathOf(text.slice(0, split)), CodePatch.pathOf(text.slice((split + 1)))];
    }
}

