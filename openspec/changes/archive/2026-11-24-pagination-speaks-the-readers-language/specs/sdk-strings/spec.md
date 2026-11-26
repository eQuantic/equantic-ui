## ADDED Requirements

### Requirement: A pager's steps and pages are named in the reader's language

`Pagination` SHALL take its previous and next steps and each page's accessible name from `SdkStrings`, in the
UI culture, as every other label of the SDK does.

#### Scenario: A pager in Brazilian Portuguese

- **WHEN** a `Pagination` of five pages on its second is built with the UI culture pt-BR
- **THEN** its steps read "Anterior" and "Próxima", and its second page is named "Página 2", where they
  read "Previous", "Next" and "Page 2" in every culture
