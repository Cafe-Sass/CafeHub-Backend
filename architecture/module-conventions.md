# Module Conventions

How CafeHub modules are organized inside the Application layer: folder layout, naming rules, the use-case contract, module communication, persistence usage, and test layout. Companion documents: `architecture/architecture-decisions.md` and `architecture/database-schema-decisions.md`.

## Purpose and scope

The CafeHub backend is a Clean Architecture Application-layer modular monolith: one deployable Web API whose business code is split into independent modules inside `CafeHub.Application` (PRD section 10.1). This guide is binding for every module: it fixes where module code lives, how types are named, how use cases are contracted, how modules communicate, where their data is stored, and how tests follow the same shape. Shared cross-cutting plumbing (messaging contracts, behaviors, results, persistence and tenancy contracts) lives in `CafeHub.Application.Abstractions` and `CafeHub.Application.Tenancy` and is not part of any module.

## Module folder layout

Every business module gets one folder under `src/CafeHub.Application/`. Subfolders and namespaces mirror each other, so a module has this shape (namespaces are block-scoped):

```
src/CafeHub.Application/
├── Abstractions/                          # shared plumbing, owned by the Application layer as a whole
│   ├── Behaviors/                         # LoggingBehavior, ValidationBehavior, MasterTransactionBehavior, TenantTransactionBehavior
│   ├── Messaging/                         # ICommand, ICommand<TResponse>, IQuery<TResponse>, handler interfaces, transaction markers
│   ├── Persistence/                       # IUnitOfWork, IMasterUnitOfWork, ITenantUnitOfWork
│   └── Results/                           # Result, Result<T>, Error, ErrorType, ValidationError, FieldError
├── Tenancy/                               # tenant context contracts (exists today; see architecture-decisions.md)
└── Example/                               # ← module folder layout, illustration ONLY - this module is not part of the codebase
    ├── Commands/
    │   ├── CreateExampleCommand.cs
    │   ├── CreateExampleCommandHandler.cs
    │   └── CreateExampleCommandValidator.cs
    ├── Queries/
    │   ├── GetExampleByIdQuery.cs
    │   └── GetExampleByIdQueryHandler.cs
    ├── DTOs/
    │   └── ExampleResponse.cs
    ├── Contracts/
    │   └── IExampleCatalog.cs             # the interface other modules and Infrastructure may consume
    └── ExampleErrors.cs                   # the module's only error factory class
```

`Example` is an illustration of the layout, not code to create; no empty module folders are added. Never put module business code into `Abstractions/`.

## Naming rules

| Kind | Rule | Example |
|---|---|---|
| Command | Verb-first, an action that changes state | `CreateExampleCommand` |
| Command with response | Same, returning `Result<TResponse>` | `ApproveExampleCommand<TResponse>` |
| Query | Read-first, read-only, no data changes | `GetExampleByIdQuery`, `ListExamplesQuery` |
| Handler | Request name + `Handler`, one per request, `sealed` | `CreateExampleCommandHandler` |
| Validator | Request name + `Validator`, next to the request it validates | `CreateExampleCommandValidator` |
| DTO | Data name + role suffix, in `DTOs/` | `ExampleResponse`, `ExampleItem` |
| Module errors | One static class, module name + `Errors`, factory methods returning `Error` | `ExampleErrors.NotFound(...)` |
| Error code | `<Module>.<Reason>` | `Example.NotFound` |
| File | One public type per file; file name equals the type name | `CreateExampleCommand.cs` |

- Namespaces mirror folders and are block-scoped: `namespace CafeHub.Application.Example { ... }`, `namespace CafeHub.Application.Example.DTOs { ... }`; file-scoped namespaces are forbidden.
- Validation failures always use the shared `ValidationError` with the fixed code `Validation.Failed`; module codes must not collide with it.

## Use-case contract

Every use case is a command or a query dispatched through MediatR, and every request and handler implements the contracts in `CafeHub.Application.Abstractions.Messaging`:

- `ICommand` (returns `Result`), `ICommand<TResponse>` (returns `Result<TResponse>`), `IQuery<TResponse>` (returns `Result<TResponse>`).
- Handlers implement `ICommandHandler<TCommand>`, `ICommandHandler<TCommand, TResponse>`, or `IQueryHandler<TQuery, TResponse>` and use constructor injection only.
- A successful result carries no error; a failed result carries exactly one `Error` from the module's `<Module>Errors` factory. Expected failures are returned, never thrown.

Every request whose handler lives in the Application assembly runs through the pipeline registered by `AddApplication`:

1. **Logging** (`LoggingBehavior<,>`) — logs the request name, outcome, and duration; never the payload or secret values.
2. **Validation** (`ValidationBehavior<,>`) — runs all `IValidator<TRequest>`; on failure it returns a failed result and the handler is never called, so no transaction is begun.
3. **Transaction** (`MasterTransactionBehavior<,>` / `TenantTransactionBehavior<,>`) — only for marked commands.
4. **Handler** — the module's own logic.

The pipeline supports only `Result`/`Result<T>` responses (each behavior constrains `TResponse : Result`): `AddApplication` registers the behaviors in the fixed order above, scans the Application assembly for handlers (`RegisterServicesFromAssembly`), and registers validators (`AddValidatorsFromAssembly`). A new module inside `CafeHub.Application` therefore needs no extra registration.

**Transaction markers.** A command that persists data implements exactly one marker, never both:

- `IMasterTransactionalCommand` — the command writes Master/Registry data; `MasterTransactionBehavior<,>` opens a transaction through `IMasterUnitOfWork`, commits on a successful result, and rolls back on a failed result or an exception (which is rethrown).
- `ITenantTransactionalCommand` — the command writes tenant data; `TenantTransactionBehavior<,>` does the same through `ITenantUnitOfWork`.

Queries and unmarked commands are never wrapped in a transaction. Application registers internal fail-closed placeholder units of work; if Infrastructure was not registered first, dispatching a marked command throws instead of silently running without a transaction.

## Module communication

- Modules communicate through **contracts and events only**: a module publishes the interfaces other code may consume in its `Contracts/` folder (for example `IExampleCatalog`), and other modules, Infrastructure, and WebApi depend only on those contracts.
- A module must never reference another module's commands, handlers, validators, DTOs, `<Module>Errors`, or folder internals; cross-module requests go through the consuming contract, not by sending another module's command.
- Module **events** are in-process MediatR notification contracts, declared by the owning module in its `Contracts/` folder, introduced with the first real cross-module need (message brokers are excluded by `architecture/dependency-policy.md`). An event carries the fact that happened; consumers never call back into the publishing module's internals.
- **No cross-module database foreign keys**: master and tenant databases are never joined across their boundary, tenant data is never joined across two cafe databases, and table ownership inside one database follows module boundaries — integrations use identifiers, not joins (PRD section 10.2).

## Persistence usage

- The **Master/Registry database** holds platform-wide data only: tenant identity and lifecycle status, subscription state, onboarding and provisioning records, and minimal tenant database reference metadata (FR-PLT-001). Today that is the Tenancy, Onboarding, and Subscriptions side.
- Each **Tenant database** holds that cafe's operational data: branches, tables, menu, orders, bills and payments, WiFi packages and tokens, inventory, attendance, reporting, and tenant audit records (FR-PLT-002, FR-ANL-002). Data placement follows `architecture/database-schema-decisions.md`; anything not yet assigned there is decided with the first entity of the module.
- Handlers never connect to a database and never choose a connection: they consume `IMasterUnitOfWork` / `ITenantUnitOfWork` (commit saves pending changes and commits the transaction) plus the module's persistence ports, which are Application interfaces added together with the first approved entity and implemented in Infrastructure.
- Tenant data access always goes through the validated tenant context: Infrastructure resolves each tenant connection from `ITenantContextAccessor.GetRequired()` through `ITenantConnectionResolver`. A missing, invalid, or inactive context throws, and there is no fallback to the Master database, a shared database, or another tenant's database (fail closed).

## Test layout

- The repository has exactly one test project, `tests/CafeHub.UnitTests` (xUnit, hand-written fakes, no database or network; see `architecture/dependency-policy.md`).
- Tests for shared plumbing live at the test project root (as the existing `ResultTests`, `ValidationBehaviorTests`, and other behavior tests do today).
- When the first module lands, its tests mirror the module structure: `tests/CafeHub.UnitTests/Example/Commands/CreateExampleCommandHandlerTests.cs`, with block-scoped namespaces that mirror the folders (`namespace CafeHub.UnitTests.Example.Commands { ... }`).
- Test-only types (fakes, request types under test) stay inside the test project next to the tests that use them; nothing test-only is added to `src/`.
- Test expectations derive from the story acceptance criteria and the PRD requirement identifiers — never from what the code currently does.

## Planned modules

All modules below are **planned**; none exists as business code yet. No empty module folder is created; a module folder appears together with its first real use case.

| Module | Scope | Status |
|---|---|---|
| Tenancy | Tenant context contracts; the tenancy abstractions already live in `src/CafeHub.Application/Tenancy/` | planned (contracts exist; use cases to come) |
| Onboarding | Registration, plan selection, approval flow, provisioning requests | planned |
| Subscriptions | Plan catalog, subscription lifecycle, initial payment states | planned |
| Identity | Owner and staff authentication and permission boundaries | planned |
| CafeConfiguration | Profile/branding, branches, tables, QR generation | planned |
| Menu | Catalog, branch availability, OCR draft import review | planned |
| Ordering | Guest and Captain-assisted orders, acceptance, Order More | planned |
| Billing | Bills, payment records, Hybrid settlement, receipts | planned |
| WiFi | Packages, token issuance | planned |
| Inventory | Operational stock only; no Sprint 1 tables | planned |
| Attendance | QR check-in/out | planned |
| Reporting | Dashboard metrics, insight cards | planned |
| Notifications | Provider-agnostic boundary contract only (PRD TBD-09) | planned |
| Printing | Local print job dispatch boundaries | planned |

## Checklist for adding a module

- Create `src/CafeHub.Application/<Module>/` together with the module's first real use case; no empty folders.
- Add the command/query, its handler, and validators: one type per file, block-scoped namespaces, `Result`/`Result<T>` contract.
- Add `<Module>Errors` with the first real error; codes follow `<Module>.<Reason>`.
- Mark persisting commands with exactly one transaction marker; inject only Application-owned abstractions.
- Add the tests under `tests/CafeHub.UnitTests/<Module>/`, derived from the acceptance criteria.
- Expose cross-module contracts and events in `Contracts/` only; never reach into another module's internals.
