# Tasks

## 1. A navigation replaces the head's metadata

- [x] 1.1 Answer the app's and the route's metadata before any gate in the navigation endpoint. Verify: `PageTitleTests.ANavigationToAPageTheServerDoesNotRender_StillAnswersItsRoutesTitleAndHead`
- [x] 1.2 Mark every metadata tag and replace the marked set in the boot's head patcher, alternates included. Verify: `PageTitleTests.EveryTagTheMetadataWrites_IsMarked_OnALoadAndANavigation`, `CultureRouteTests` reads the translation group in a navigation, and on the dashboard sample a navigation from `/charts` to `/clock` leaves only `/clock`'s alternates
