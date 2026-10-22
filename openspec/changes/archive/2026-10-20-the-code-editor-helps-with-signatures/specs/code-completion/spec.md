# Spec Delta

## ADDED Requirements

### Requirement: Each answer shows as it arrives

The completion SHALL apply each provider's answer as it arrives, and SHALL list the offers in the order
of `Providers` whatever the order the answers land in. When an answer lands, the selected entry SHALL stay
selected if it is still listed. The list SHALL close for want of offers only once every provider asked has
answered and none offered anything, and `IsLoading` SHALL be true while a provider asked is still out. A
list opening SHALL raise `Changed` once its requests are out and before any answer lands, so `IsLoading`
says so to whoever hears it. An answer that withdraws every entry its provider offered SHALL take them away
as it lands, while another provider is still out too. Each incomplete answer SHALL be asked for again once
the word is not the one its provider was asked for last, the word of its answer or of a request of its
still out, so a request out for another word is replaced even when the answer in hand is for the word
typed.

#### Scenario: A provider that never answers

- **WHEN** one provider never answers and another answers `Column` at once, and `Co` is typed
- **THEN** the list shows `Column`, and `IsLoading` is true

#### Scenario: A later answer

- **WHEN** a list shows a first provider's `Column`, `Column` is selected, and a second provider then
  answers `Color` and `Count`
- **THEN** the list shows all three, ranked as one answer would rank them, and `Column` is still selected

#### Scenario: An answer withdrawing every entry

- **WHEN** a list shows a first provider's `Column` for `C`, a second provider has not answered, `o` is
  typed, and the first provider, asked again, answers nothing
- **THEN** the list closes at once, `IsLoading` is true, and the second provider's answer still shows when
  it lands

#### Scenario: A slow incomplete answer for a word left behind

- **WHEN** a quick provider and a slow one are asked for `C`, both incomplete, `o` is typed and the quick one
  is asked again, and the slow one then answers for `C`
- **THEN** the slow provider is asked again for `Co`, once

#### Scenario: A letter typed, then backspaced

- **WHEN** a provider answers `Cat` and `Column` for `C`, incomplete, `o` is typed and the provider is
  asked for `Co`, and Backspace is pressed before it answers
- **THEN** the provider is asked again for `C`, its request for `Co` is cancelled, and that request's
  answer, if it lands, is dropped

#### Scenario: A list heard of as loading

- **WHEN** ⌃Space asks a provider that has not answered
- **THEN** `Changed` is raised once, with `IsLoading` true, and again with it false when the provider
  answers

## MODIFIED Requirements

### Requirement: An answer nobody waits for is dropped

An answer SHALL be applied only while its list is open and its request is the newest made of its own
provider; an older one SHALL be dropped however late it arrives, and its request SHALL be cancelled.
Asking a provider again SHALL cancel that provider's request alone: the answer another provider still owes
SHALL still be applied.

#### Scenario: A list closed before its answer

- **WHEN** a word is started, Escape pressed, and then the provider answers
- **THEN** no list opens, and the request's token was cancelled

#### Scenario: Two requests answered out of order

- **WHEN** `a` and then `.` start two requests, and the second is answered before the first
- **THEN** the list shows the second's answer only

#### Scenario: An incomplete answer asked again beside a slow provider

- **WHEN** one provider answers incomplete at once and another has not answered, and a letter is typed
- **THEN** the first is asked again, the second's request is not cancelled, and its answer, when it comes,
  is listed
