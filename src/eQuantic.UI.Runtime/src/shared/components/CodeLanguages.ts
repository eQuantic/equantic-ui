import { CSharpLanguage, JsonLanguage, PlainTextLanguage, PythonLanguage, TypeScriptLanguage, XmlLanguage } from "../runtime-exports";

export class CodeLanguages {
    static _cSharp: any | undefined;

    static get cSharp(): any {
        return CodeLanguages._cSharp ??= new CSharpLanguage();
    }

    static _typeScript: any | undefined;

    static get typeScript(): any {
        return CodeLanguages._typeScript ??= new TypeScriptLanguage();
    }

    static _python: any | undefined;

    static get python(): any {
        return CodeLanguages._python ??= new PythonLanguage();
    }

    static _json: any | undefined;

    static get json(): any {
        return CodeLanguages._json ??= new JsonLanguage();
    }

    static _xml: any | undefined;

    static get xml(): any {
        return CodeLanguages._xml ??= new XmlLanguage();
    }

    static _plainText: any | undefined;

    static get plainText(): any {
        return CodeLanguages._plainText ??= new PlainTextLanguage();
    }

    static _known: Record<string, any> | undefined;

    static get known(): Record<string, any> {
        return CodeLanguages._known ??= { ['c#']: CodeLanguages.cSharp, ['csharp']: CodeLanguages.cSharp, ['cs']: CodeLanguages.cSharp, ['typescript']: CodeLanguages.typeScript, ['ts']: CodeLanguages.typeScript, ['javascript']: CodeLanguages.typeScript, ['js']: CodeLanguages.typeScript, ['tsx']: CodeLanguages.typeScript, ['jsx']: CodeLanguages.typeScript, ['python']: CodeLanguages.python, ['py']: CodeLanguages.python, ['json']: CodeLanguages.json, ['xml']: CodeLanguages.xml, ['csproj']: CodeLanguages.xml, ['html']: CodeLanguages.xml, ['plist']: CodeLanguages.xml, ['text']: CodeLanguages.plainText, ['txt']: CodeLanguages.plainText, ['plain']: CodeLanguages.plainText };
    }

    static register(name: string, language: any) {
        return CodeLanguages.known[name] = language;
    }

    static for(name: string | null) {
        let language: any; if ((!name || !name.trim())) return CodeLanguages.plainText;
        let key = (_s => { const _c = '.'; let _i = 0; while (_i < _s.length && _c.includes(_s[_i])) _i++; return _s.slice(_i); })(name);
        return (Object.prototype.hasOwnProperty.call(CodeLanguages.known, key) ? ((language = CodeLanguages.known[key]), true) : false) ? language : CodeLanguages.plainText;
    }
}

