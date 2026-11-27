import { CancellationToken, CodeDocument, CodeLocation, CodePosition } from "../runtime-exports";

export interface ICodeReferenceProvider {
    referencesAsync(document: CodeDocument, position: CodePosition, cancellation: CancellationToken): Promise<CodeLocation[]>;
}

export const ICodeReferenceProvider = undefined;
