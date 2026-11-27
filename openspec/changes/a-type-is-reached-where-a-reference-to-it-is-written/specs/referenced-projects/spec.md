## MODIFIED Requirements

### Requirement: Only what the browser code reaches is compiled

A type of a referenced project that no written module names SHALL NOT be compiled or written, and
SHALL raise no diagnostic, whatever it contains. A type is reached when a written module writes a
reference to it, and for no other reason: code that does not cross, a name the build writes nothing for
(inside `nameof`, in an attribute, or a constant written as its value), and syntax that names a type and
writes nothing of it (a cast of a null, a `default`, a delegate's type, a constant in the old `is` form, a
member of an enum nested in the type) reach nothing. An import of a sibling's name SHALL mean the types
whose twin the module's writers wrote under that name, and where a writer wrote the name and recorded no
type, every type of the name the module's symbols name.

#### Scenario: A referenced project that holds server code

- **WHEN** the referenced project also declares a class that reads a file with `System.IO`, and no
  page names it
- **THEN** the build succeeds and the bundle holds no module of that class

#### Scenario: A Server Action names the referenced project's server code

- **WHEN** a page's Server Action builds that class in its body, and nothing else of the page names it
- **THEN** the build succeeds and the bundle holds no module of that class

#### Scenario: A Server Action maps a referenced project's type of the app's name

- **WHEN** a page shows the app's own `Order`, and its Server Action, or a `[ServerOnly]` method of
  it, builds that `Order` from the referenced project's `Order`, a class that reads a file
- **THEN** the build succeeds with the app's `Order` as the module, and the referenced project's is
  not compiled

#### Scenario: A Server Action's answer is named by its return type alone

- **WHEN** a Server Action answers a record of the referenced project, and nothing else of the page
  names that record
- **THEN** the page's module imports the record, and the bundle holds its module

#### Scenario: A constant read off a referenced project's class

- **WHEN** a static class of the app declares `const string Serif = Theme.Serif;`, where `Theme` is a
  class of the referenced project that also reads a file
- **THEN** the app's module holds the constant's value, imports nothing of `Theme`, and the build
  succeeds with no module of `Theme`

#### Scenario: A name written nowhere names a referenced project's type of the app's name

- **WHEN** a page shows the app's own `Order`, and names the referenced project's `Order`, a class
  that reads a file, only inside `nameof`, in an attribute, or by reading a constant of it
- **THEN** the build succeeds with the app's `Order` as the module, the name and the constant are
  written as text, and the referenced project's `Order` is not compiled

#### Scenario: A constant written as a reference

- **WHEN** a page reads a constant of a referenced project's static class by its bare name, under
  `using static`
- **THEN** the page's module reads it off that class's twin and imports the class, and the bundle
  holds its module

#### Scenario: Syntax that writes no reference names a referenced project's type of the app's name

- **WHEN** a page shows the app's own `Order`, and names the referenced project's `Order`, a class that
  reads a file, only in a cast of a null, a `default`, a delegate's type, a constant in the old `is` form
  or a member of an enum nested in it
- **THEN** the build succeeds with the app's `Order` as the module, and the referenced project's is not
  compiled

#### Scenario: The app's own type is written another way beside such syntax

- **WHEN** the page writes the app's `Order` as a parameter's annotation, a property's, a static read off
  the type or a Server Action's answer, and casts a null to the referenced project's `Order`
- **THEN** the build succeeds with the app's `Order` as the module, and the referenced project's is not
  compiled
