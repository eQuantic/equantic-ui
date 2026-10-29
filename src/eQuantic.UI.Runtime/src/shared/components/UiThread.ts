import { UiDispatcher } from "../runtime-exports";

export class UiThread {
    static run(work: () => void) {
        let dispatcher: any; 
        if ((($0) => (($0 != null && $0.isOnUiThread === false) && (dispatcher = $0, true)))(UiDispatcher.current)) dispatcher.post(work); else work();
    }
}

