## ADDED Requirements

### Requirement: While the server loads a page's data, only what loads keeps its instance

While `ServerDataRounds` runs, a position SHALL keep the stateful instance it holds when its parent's
build hands it a fresh one only if that instance loads data (`IServerPrefetch`); any other stateful
instance SHALL give the position to the fresh one, which leaves the old instance's position and enters
the new one's as a first build does. `BuildOwner.KeepsInstance` SHALL say which instances a position
keeps; the rounds SHALL arm it and put back what it held when they end, and a tree drawn for a person
SHALL leave it null, which keeps every instance.

#### Scenario: A stateful section handed what its page loads

- **WHEN** a page whose prefetch loads `675617` builds a stateful section that loads nothing, handing it
  the loaded value through its constructor, and the server renders the page
- **THEN** the served HTML shows `Section: 675617`, not the page's default

#### Scenario: A stateful loader under a page that loads

- **WHEN** a page that loads composes a stateful component that loads too
- **THEN** each is asked once, and the component draws what it loaded

#### Scenario: Once the page has loaded

- **WHEN** the tester pumps a page through its rounds and then pumps the page again with a fresh instance
- **THEN** the stateful section keeps its instance, and nothing mounts or unmounts
