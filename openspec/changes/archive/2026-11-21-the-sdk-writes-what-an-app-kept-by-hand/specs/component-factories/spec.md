## ADDED Requirements

### Requirement: A referenced project's components get factories

The factory surface SHALL give a factory to every public component, with the rules of the app's own
components, of an assembly the app references that came from a project the build compiles from source
(not the SDK's own projects, not an icon pack), and to none of an assembly that came from a package. The
surface SHALL stay in the namespace the app's own components share, or, for an app with none, in the
namespace its assembly is named by. A component of the app and one of a referenced project with one name
SHALL leave the factory to the app's, with EQ3102 naming the other.

#### Scenario: A library's component composed by a page

- **WHEN** a page of an app that references the project `Brand.Common` calls `Wordmark("Since 1998")`,
  and `Brand.Common` declares a public `Wordmark(string tagline = "")`
- **THEN** the surface holds `Wordmark(string tagline = "")` building `global::Brand.Common.Wordmark`, and
  the app compiles

#### Scenario: A package's component

- **WHEN** `Brand.Common` came from a package rather than a project
- **THEN** the surface holds no `Wordmark`, and the page's call does not compile

#### Scenario: The shared library sample

- **WHEN** `samples/SharedLibrary/SharedLibrary.Web` composes `Wordmark()` and is built
- **THEN** its `AppUI` builds the library's `Wordmark`, its module draws it, and the page shows it in each
  of its languages
