import { $eq } from "../runtime-exports";

export class SortWords {
    static fill(text: string, holes: string[]) {
        let filled = $eq.text.stringBuilder(text.length);
        let at = 0;
        while (at < text.length) {
            let close = text[at] === '{' ? $eq.text.indexOfChar(text, '}', at + 1 | 0) : -1;
            let hole = close > (at + 1 | 0) ? SortWords.holeBetween(text, at + 1 | 0, close) : -1;
            if (hole >= 0 && hole < holes.length) {
                filled.append(holes[hole]);
                at = close + 1 | 0;
                continue;
            }
            filled.append(text[at]);
            at = at + 1 | 0;
        }
        return filled.toString();
    }

    static holeBetween(text: string, start: number, end: number) {
        let hole = 0;
        for (let at = start; at < end; at = at + 1 | 0) {
            let digit = text[at].charCodeAt(0) - 48 | 0;
            if (digit < 0 || digit > 9) return -1;
            hole = Math.imul(hole, 10) + digit | 0;
        }
        return hole;
    }
}

