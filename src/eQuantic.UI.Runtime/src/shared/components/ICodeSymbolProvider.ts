import { CancellationToken, CodeDocument, CodeSymbol } from "../runtime-exports";

export interface ICodeSymbolProvider {
    symbolsAsync(document: CodeDocument, cancellation: CancellationToken): Promise<CodeSymbol[]>;
}

export const ICodeSymbolProvider = undefined;
