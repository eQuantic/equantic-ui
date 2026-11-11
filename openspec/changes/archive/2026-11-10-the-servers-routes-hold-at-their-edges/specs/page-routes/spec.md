## ADDED Requirements

### Requirement: The module route answers what it finds and echoes nothing it did not

`/_equantic/{name}.js` SHALL serve a module it finds under the web root or in the local directory with
200 and the JavaScript type, and SHALL answer a missing one with 404 in plain text that names the request
only in the Development environment.

#### Scenario: A module in the local directory

- **WHEN** a module exists only in the local directory's `wwwroot/_equantic`
- **THEN** it is served with 200 and the JavaScript type

#### Scenario: A missing module in Production

- **WHEN** a module that does not exist is asked for in Production
- **THEN** the answer is 404 and its body does not hold the name asked for

### Requirement: The redirect to a prefixed language writes its path encoded

The redirect that takes a bare URL to its prefixed language SHALL write the path as a URL writes it.

#### Scenario: A path with an accent

- **WHEN** a reader in a prefixed language asks for a bare path holding a non-ASCII character
- **THEN** the answer is 302 to the prefixed path, percent-encoded
