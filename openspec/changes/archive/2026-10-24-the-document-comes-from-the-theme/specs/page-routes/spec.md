# Spec Delta

## ADDED Requirements

### Requirement: The 500 page answers only a render that broke

The app's 500 page SHALL answer for a page only when the server attempted to render it and the render
failed. A page that opted out of server rendering (`DisableSsr`) SHALL be answered 200 with its
placeholder, in every environment.

#### Scenario: A client-only page in an app with a 500 page

- **WHEN** an app in production registers a 500 page, and a visitor asks for a page marked `DisableSsr`
- **THEN** the answer is 200 with the page's placeholder, and the 500 page is not drawn
