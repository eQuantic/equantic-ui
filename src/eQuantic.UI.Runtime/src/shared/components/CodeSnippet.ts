import { CodeSnippetParser, CodeSnippetPart } from "../runtime-exports";

export class CodeSnippet {
    constructor(body: string, parts: CodeSnippetPart[]) {
        this.body = null!;
        this.parts = null!;
        this.body = body;
        this.parts = parts;
    }

    body!: string;
    parts!: CodeSnippetPart[];

    static parse(body: string) {
        return new CodeSnippet(body, new CodeSnippetParser(body).parse());
    }

    toString() {
        return this.body;
    }
}

