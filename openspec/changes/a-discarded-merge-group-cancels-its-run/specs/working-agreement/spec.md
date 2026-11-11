## ADDED Requirements

### Requirement: A merge group the queue discarded does not keep its run

The CI SHALL cancel the run of every merge group the queue discarded, which it tells by the group's ref being deleted while its commit is not on main. Every merge group's run, every push to main and a run by hand SHALL sweep the workflow's unfinished merge-group runs, the sweeping run's own included and cancelled last. A run whose ref still exists, or whose commit is on main, SHALL NOT be cancelled. A lookup that fails any other way than a 404 SHALL cancel nothing and SHALL fail the sweep's step, which SHALL NOT fail its run. The sweep SHALL NOT be a required check.

#### Scenario: An entry ahead fails

- **WHEN** a group is discarded and the queue builds a new group behind the failed entry
- **THEN** the new group's run cancels the discarded group's run, and every live group's run goes on

#### Scenario: A lookup fails

- **WHEN** the lookup of a group's ref answers 500
- **THEN** that run is left as it is, and the sweep fails, naming it
