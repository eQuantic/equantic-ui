import { $CodeDefinitionKindValue, CancellationToken, CodeDocument, CodeLocation, CodePosition } from "../runtime-exports";

export interface ICodeDefinitionProvider {
    definitionsAsync(document: CodeDocument, position: CodePosition, kind: $CodeDefinitionKindValue, cancellation: CancellationToken): Promise<CodeLocation[]>;
}

export const ICodeDefinitionProvider = undefined;
