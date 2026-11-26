import { CancellationToken, CodeDocument, CodeFold } from "../runtime-exports";

export interface ICodeFoldProvider {
    foldsAsync(document: CodeDocument, cancellation: CancellationToken): Promise<CodeFold[]>;
}

export const ICodeFoldProvider = undefined;
