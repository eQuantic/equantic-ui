import { $eq, GestureRecognizerFactoryWithHandlers, GestureRouter } from "@equantic/runtime";
import { ArenaProbe } from "./ArenaProbe";
import { HoldThenDrag } from "./HoldThenDrag";

export class FactoryTypeChange {
    static run() {
        let heard: string[] = [];
        let router = new GestureRouter();
        let first = $eq.collections.item(router.recognizers('place', [$eq.closing(new GestureRecognizerFactoryWithHandlers(() => {
            $eq.collections.listAdd(heard, 'probe.create');
            return new ArenaProbe('probe', heard);
        }, (_) => $eq.collections.listAdd(heard, 'probe.initialize')), 'global::eQuantic.UI.Web.Tests.Fixtures.ArenaProbe')]), 0);
        let second = $eq.collections.item(router.recognizers('place', [$eq.closing(new GestureRecognizerFactoryWithHandlers(() => {
            $eq.collections.listAdd(heard, 'drag.create');
            return new HoldThenDrag();
        }, (drag) => $eq.collections.listAdd(heard, (drag === first) ? 'drag.initialize the probe' : 'drag.initialize its own')), 'global::eQuantic.Console.HoldThenDrag')]), 0);
        $eq.collections.listAdd(heard, (first === second) ? 'kept' : 'made new');
        return heard;
    }
}

