## ADDED Requirements

### Requirement: Hydration records what the browser drew differently

Hydration SHALL record, in its result, each text and attribute of the server's markup it wrote the client's
over, and each element or text the server wrote past the client's children, with the element's path, what the
server wrote and what the client's tree says. The classes of an element SHALL be compared as a set, and a
`data-eq-*` marker SHALL be no difference. The first load SHALL write what it did on `window.__EQ_HYDRATION__`:
the page the server drew, the page the router chose, whether the markup was adopted, why it was drawn over,
and the differences.

#### Scenario: A label the client draws empty

- **WHEN** the server wrote `<p>Our cakes</p>` and the client's tree holds that paragraph with an empty text
- **THEN** the result records a text difference at the paragraph, 'Our cakes' on the server and '' in the client

#### Scenario: Classes in another order

- **WHEN** the server wrote `class="b a"` and the client's tree says `class="a b"`
- **THEN** the result records no difference

#### Scenario: Every page of the dashboard sample

- **WHEN** each page of `samples/DefaultUIDashboard` is hydrated at 1280 and at 400 pixels
- **THEN** none records a difference or an error
