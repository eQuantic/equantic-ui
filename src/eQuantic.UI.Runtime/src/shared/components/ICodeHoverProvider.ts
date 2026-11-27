import { CancellationToken, CodeDocument, CodeHover, CodePosition } from "../runtime-exports";

export interface ICodeHoverProvider {
    hoverAsync(document: CodeDocument, position: CodePosition, cancellation: CancellationToken): Promise<CodeHover | null>;
}

export const ICodeHoverProvider = undefined;
