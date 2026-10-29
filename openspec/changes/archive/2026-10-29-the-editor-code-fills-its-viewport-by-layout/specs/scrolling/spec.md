## ADDED Requirements

### Requirement: A scroll view's content can fill its viewport

A `ScrollView` whose `FillViewport` is set SHALL lay its content out never smaller than its viewport, on the
web and on Photon, in the layout that sizes the viewport and with no size reported back: the content SHALL
take the viewport's extent on each axis on which it is smaller, and SHALL scroll only where it is longer. A size
the content states on an axis, a fixed one or the window's, SHALL stand. The viewport SHALL be the
room inside the scroller, with no scrollbar that takes space in it, so content that fills it never makes an
enclosing scroller scroll. A scroll view whose parent stretches it on an axis it hugs SHALL take the stretched
size on both targets.

#### Scenario: A short content

- **WHEN** a vertical scroll view 100 tall that fills its viewport holds a column 60 tall
- **THEN** the column is 100 tall and the scroll view has nothing to scroll

#### Scenario: A long content

- **WHEN** the same scroll view holds a column 240 tall
- **THEN** the column is 240 tall and the scroll view scrolls by 140

#### Scenario: Sideways, inside a stretched row

- **WHEN** a row 90 tall that stretches its children holds a horizontal scroll view that fills its viewport,
  around a box of 50 by 20
- **THEN** the scroll view is 90 tall and its content 90 tall
