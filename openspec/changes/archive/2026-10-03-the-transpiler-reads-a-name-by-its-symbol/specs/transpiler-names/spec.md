## ADDED Requirements

### Requirement: A base type is named after its twin

The base a class, a component, a primitive or a record extends SHALL be emitted under its twin's
name, the class's own simple name read from its symbol, whatever the source's spelling of it: a
namespace, an alias or `global::`. The module SHALL import that name as it imports every other.

#### Scenario: A primitive whose base is qualified

- **WHEN** an app declares `public sealed class Tag : eQuantic.UI.Web.HtmlElement`
- **THEN** its module declares `class Tag extends HtmlElement` and imports `HtmlElement`

#### Scenario: A base through an alias

- **WHEN** an app writes `using Pail = App.Models.Bucket;` and declares `public class Bin : Pail`
- **THEN** its module declares `class Bin extends Bucket`

### Requirement: A type's own Count is its own

A `Count` read SHALL be spelled by the receiver's symbol: an array and a type of .NET's own
namespaces (`System`, `Microsoft`), a list or a lookup among them, read the array's `length` (or the
size or count of the class the browser holds it as), and a type declared in the app or in a library
it references reads its twin's `count`.

#### Scenario: A library's type

- **WHEN** a component reads `_tally.Count` of `Lib.Tally { public int Count { get; set; } }`, a type of
  a library the app references, and `_rows.Count` of a `List<int>`
- **THEN** its module reads `this._tally.count` and `this._rows.length`
