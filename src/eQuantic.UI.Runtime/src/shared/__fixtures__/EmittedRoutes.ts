import { $eq } from "@equantic/runtime";

export class EmittedRoutes {
    static checkout: string = '/checkout';

    static pay(order: string) {
        return `${EmittedRoutes.checkout}?order=${$eq.uri.escapeData(order)}`;
    }
}

