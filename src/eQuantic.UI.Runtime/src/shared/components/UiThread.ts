import { UiDispatcher } from "../runtime-exports";

export class UiThread {
    static run(work: () => void) {
        let dispatcher: any; 
        if (((UiDispatcher.current != null && UiDispatcher.current.isOnUiThread === false) && (dispatcher = UiDispatcher.current, true))) dispatcher.post(work); else work();
    }
}

