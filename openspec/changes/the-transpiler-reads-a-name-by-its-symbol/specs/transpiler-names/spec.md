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
