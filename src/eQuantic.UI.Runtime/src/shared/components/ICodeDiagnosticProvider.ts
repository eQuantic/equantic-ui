import { CancellationToken, CodeDiagnostic, CodeDocument } from "../runtime-exports";

export interface ICodeDiagnosticProvider {
    diagnoseAsync(document: CodeDocument, cancellation: CancellationToken): Promise<CodeDiagnostic[]>;
}

export const ICodeDiagnosticProvider = undefined;
