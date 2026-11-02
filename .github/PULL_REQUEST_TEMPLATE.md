## Task
**CFMD-XXX** - `TaskName`

## What changed
<!-- Short list of the real changes: files, types, behavior. No marketing text. -->

## Why
<!-- The acceptance criteria this PR satisfies. Cite Jira AC numbers and PRD requirement IDs only; do not paste PRD text. -->

## API & Contracts
<!-- New or changed endpoints, request and response shapes, error codes. Write N/A if none. -->

## Database / Tenant impact
<!-- Tables, migrations, indexes, reviewed SQL script path, tenant isolation effects. Write N/A if none. -->

## Tests
<!-- AC to test table. List test files changed. Baseline count before the first commit and final count. State "Test files changed: none" when that is the case. -->

| Acceptance criterion | Test |
|---|---|
|  |  |

Baseline tests: 
Final tests: 

## Verification results
<!-- Allowed statuses only: Passed, Failed, Not Run, Not Applicable. Never write Passed for a command that did not run. Give the reason for every Not Run. -->

| Check | Status | Notes |
|---|---|---|
| `dotnet restore` |  |  |
| `dotnet build -c Release` (warnings: ) |  |  |
| Unit tests |  |  |
| `dotnet format --verify-no-changes` |  |  |
| `git diff --check` |  |  |
| Real PostgreSQL / running host checks |  |  |

## Assumptions and open items
<!-- Every decision not in the AC or the PRD, labelled "Proposed, pending owner confirmation" with the reason and the rejected alternative. Open items that need the owner. -->

## Risks & Impact
<!-- What could break, what depends on this, security notes (secrets, logging, tenant isolation). -->

## Review focus
<!-- The 3 to 5 places the reviewer should look first. -->

## Checklist
- [ ] Branch name and PR title are identical (`Feature/<number>_SA_<PascalCase>`)
- [ ] Every commit follows `<type>(<scope>): <description>` and has the `Refs: CFMD-XXX` footer
- [ ] Every commit builds; no secret, password, hash, or token in code, logs, tests, or docs
- [ ] Tests derive from the acceptance criteria and none was weakened; test count did not drop
- [ ] Block-scoped namespaces, one type per file, no TODO comments
- [ ] No new package without dependency-policy approval
- [ ] Documentation and ERD updated where behavior or tables changed
