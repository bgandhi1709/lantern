# Coding standards

Read before writing code and again during review. The architecture rules below are **hard rules**: a change that breaks
one is refused in review unless an ADR and a decision-log entry that change the rule land first (the divergence guard
in `docs/agents/workflow.md`). The layering follows the user's `wf` architecture ([ADR-0005](docs/adr/0005-layered-like-wf.md)).
Anything a linter or CI check can enforce belongs there instead; the `Guardrails` step in `.github/workflows/api.yml`
already checks script modes and keeps test code out of `apps/` and `libs/`.

**Consistency beats preference.** Before writing a class, open the nearest existing one of the same kind and copy its
shape, naming, folder and registration. The reference examples are listed at the end of the Architecture section.

## Architecture

### 1. Where code lives

| Project | Owns | Never contains |
|---|---|---|
| `apps/lantern-api/Lantern.Api` | Controllers, API models, `ApiProfile` (API ↔ service mapping), `HttpIdentityResolver`, `ProblemExceptionHandler`, rate limits, the host's `Program.cs` | Business rules, storage, Azure SDK clients, service models built by hand |
| `apps/lantern-api/Lantern.Functions` | Triggers, `Handler/` (dispatcher, `ActionHandler<T>`, one handler per `ActionType`), `FunctionsModule` | HTTP, business rules a service owns |
| `libs/Lantern.Base` | Services (`I{Model}Service`), validators, normalizers, `BaseModule` | Azure SDK types, row keys, ciphertext, HTTP types, handlers |
| `libs/Lantern.Core` | Service models, every contract (`IServiceBase<T>`, `IRepositoryBase<T>`, `I{Model}Repository`, `I{Thing}Store`, `IActionLedger`), `ServiceBase<T>`, exceptions, `{Section}Options`, `IIdentityResolver`, crypto, generic external services (`IServiceBusService`, `IKeyVaultClient`), action types, payloads and publisher, `CoreModule` | Implementations of storage contracts, entities, anything app-specific |
| `libs/Lantern.Repository` | Entities (internal), `UnitOfWork<TEntity>`, `BaseRepository<TModel, TEntity>`, repositories, stores, field encryption, `RepositoryProfile`, `RepositoryModule` | Business rules, HTTP, service orchestration |
| `tests/<app>/` | Every test project and every local stand-in (Key Vault stand-in, seeder, Docker host) | Production code |

`apps/` and `libs/` hold production code only. Test projects mirror the app they test (`tests/lantern-api/` ↔
`apps/lantern-api/`), and their folders mirror the production folders.

### 2. Dependency direction

- `Lantern.Api` → `Lantern.Base` → `Lantern.Repository` → `Lantern.Core`. `Lantern.Functions` → `Lantern.Base`.
- `Lantern.Core` references no project of ours. `Lantern.Base` uses `Lantern.Repository` only through Core's
  interfaces; the reference exists so `BaseModule` can call `AddLanternRepository`.
- App code never uses a `Lantern.Repository` type. `Lantern.Functions` never references `Lantern.Api`, and the reverse.
- A contract (interface) lives in Core; its implementation lives in the layer that owns the concern.

### 3. One model per layer, never crossing

- **API models** (`Lantern.Api/Models`) are what the app sees. They never leave `Lantern.Api`; a service never takes or
  returns one. What the app may see is decided here alone.
- **Service models** (`Lantern.Core/Models`) are what services and repositories exchange. They implement
  `IFamilyModel` when they belong to a Family and carry no storage detail: no partition or row key, no ETag, no
  ciphertext.
- **Entities** (`Lantern.Repository/Entities`) are `internal` and never returned from a repository.
- Mapping is Mapster only, through one `IRegister` per boundary: `ApiProfile` (API ↔ service) and `RepositoryProfile`
  (service ↔ entity), merged into the one `IMapper`. Never write `ToX`/`FromX` mapping methods or copy properties by
  hand. `*Profile` is reserved for these Mapster classes.

### 4. Reuse before you write

Use these in order. A pull request that hand-writes what one of them already does is refused.

1. **A new resource in a Family**: a service model in `Core/Models` (implementing `IFamilyModel`), an entity in
   `Repository/Entities` (deriving `TableEntityBase`), a repository deriving `BaseRepository<TModel, TEntity>` that
   supplies only `RowKeyPrefix`, `RowKey(id)` and `IdOf(model)`, its interface in `Core/Repository`, and one line in
   `RepositoryModule`. The open generic `IServiceBase<T>` → `ServiceBase<T>` then serves it with no service code.
2. **A service with logic beyond scoping**: `I{Model}Service : IServiceBase<{Model}>` and
   `{Model}Service : ServiceBase<{Model}, I{Model}Repository>`. Override only the operations whose rules differ, and use
   the base's `Repository`, `Families`, `Identity` and `FamilyIdAsync`; never re-inject what the base already holds.
3. **A controller**: derive `ResourceControllerBase<TModel, TServiceModel>`, map with `Mapper`/`ToModel`, call one
   service method, and pick the status code.
4. **Work that must not run inside a request**: add a value to `ActionType` (kebab-case wire name) and a payload record
   in `Core/Actions`; the service records it with `IActionPublisher.RecordAsync` **before** its own writes and calls
   `SendAsync` after them; a handler in `Lantern.Functions/Handler` derives `ActionHandler<TPayload>` and is registered
   in `FunctionsModule`. Never add a queue, a trigger, a timer or a `BackgroundService` for it: the one
   `workspace-events` queue and dispatcher carry every event.
5. **An external resource** (a queue, a vault, a third-party API): one generic service in Core that knows nothing about
   what it carries, as `IServiceBusService` sends any model to any queue. Domain code wraps it; it never wraps the
   domain.
6. **A helper**: search first. If two classes need the same logic, it moves into the base class or a service that
   both inject, not into a second copy and not into a static helper.

Add an **interactor** (in `Lantern.Base/Interactors`) only when one service has grown too complex, the way wf's
`ReportInteractor` sits over its Request and Response services, and say why in the PR. Never add one by default.

### 5. Separation of concerns

- **Controller**: binds and maps API models, calls one service method, returns the status code. No `try`/`catch`
  (`ProblemExceptionHandler` maps exceptions), no business checks, and never a Family id from the request.
- **Service**: business rules, `IValidator<T>` validation, normalization, the order of repository calls and of action
  recording. No Azure SDK type, row key, ETag, ciphertext or HTTP type.
- **Repository and store**: storage only: keys, ETags, batches, encryption, entity mapping. The only business rule
  allowed is one that needs storage atomicity (the limit of six Children under the Family row's ETag). Only
  `UnitOfWork<TEntity>` sends table requests, and only a store (`WorkspaceStore`) sends Blob requests.
- **Handler**: finishes one action type with idempotent steps through repositories and stores. The base class checks
  the ledger and completes it; a handler never touches the ledger itself.
- **Family scope** comes only from `IIdentityResolver` through `ServiceBase.FamilyIdAsync`, and a model's `FamilyId`
  is overwritten from it on every write.
- **Encryption** happens only in the repository, by marking entity columns `[Encrypted("column")]`. Crypto primitives
  live only in `CryptoService`, and Key Vault is reached only through `IKeyVaultClient`.

### 6. Registration (DI)

- Each project registers its own types in one `{Layer}Module.AddLantern{Layer}()` extension. Hosts call
  `AddLanternBase()` (plus `AddLanternFunctions()` in Functions) and add only host concerns in `Program.cs`.
- Library registrations use `TryAdd*`, so a host or test can replace one.
- Lifetimes: repositories, services, the dispatcher, handlers and `FamilyKeyRing` are scoped (`FamilyKeyRing` so a
  Family key is unwrapped at most once per request); units of work, the ledger, stores, the publisher and Azure clients
  are singletons.
- Constructor injection with primary constructors. No service locator outside DI factory lambdas and the rate-limit
  policy.

### 7. Naming

| Kind | Pattern | Example |
|---|---|---|
| Service model | `{Noun}` | `Child`, `Family` |
| Entity | `{Noun}Entity` | `ChildEntity` |
| Repository | `I{Noun}Repository` / `{Noun}Repository` | `ChildRepository` |
| Store (Blob) | `I{Noun}Store` / `{Noun}Store` | `WorkspaceStore` |
| Service | `I{Noun}Service` / `{Noun}Service` | `ChildService` |
| Validator | `{Noun}Validator : IValidator<{Noun}>` | `ChildValidator` |
| API model | response `{Noun}Model`, input `{Noun}AddModel` / `{Noun}UpdateModel` / `{Noun}SaveModel`, a command `{Noun}{Verb}Request` | `ChildAddModel`, `FamilyRegisterRequest` |
| Controller | `{Nouns}Controller` in `Controllers/V{n}` | `ChildrenController` |
| Action | `ActionType.{Verb}{Noun}`, wire `{verb}-{noun}`, `{Verb}{Noun}Payload`, `{Verb}{Noun}Handler` | `RemoveWorkspace`, `remove-workspace` |
| Options | `{Section}Options` with `const string SectionName` | `StorageOptions` |
| DI module | `{Layer}Module.AddLantern{Layer}()` | `RepositoryModule` |
| Logging | one `static partial` log class per project with `[LoggerMessage]`; an event id is unique across the solution and logs ids and counts only, never text a Parent entered | `BaseLog`, `FunctionLog` |

- Domain words come from `GLOSSARY.md` only: Family, Parent, Child, Class, Workspace. A new domain word goes into the
  glossary in the same PR.
- No `View`, `Record`, `Body`, `Dto`, `Helper`, `Manager` or `Utils` suffixes, and none of the suffixes CA1711 reserves
  (`Collection`, `Queue` and so on).
- One type per file, the file named after the type, the namespace matching the folder.

### 8. Configuration

- **No environment value in code or `appsettings.json`.** Every name and tuning value is written once in
  `infra/params/lantern.<env>.bicepparam` (secrets in Key Vault), and Bicep passes it to the apps as an env var
  ([D36](docs/ideation/decision-log.md)). `appsettings.json` holds empty keys only; local values live in
  `appsettings.Development.json` and `deploy/local`.
- Every setting binds to a `{Section}Options` class with DataAnnotations, **no default value**, and `ValidateOnStart()`
  in the host that uses it, so a missing setting stops the app instead of falling back silently.
- GitHub Actions carries only per-run values (image tags, port) and its own sign-in.
- **Never configurable**, by design: storage formats (row keys such as `profile`, the `[Encrypted]` column names and
  the cipher context, `class.json`), the wire contract (action wire names, problem codes, routes, the
  `x-api-version` header) and product rules (six Children, ages 3 to 18, body and text limits). These are constants or
  enums in the class that owns them; changing one is a code change with tests or a migration.

### 9. Errors

- A failure the caller must see is an exception from `Core/Exceptions`, mapped to a problem code in
  `ProblemExceptionHandler`. Never a `bool`, a `null` or a status code from a service or repository.
- Catch only a specific, expected condition, and say why in one line (a 409 that means "already recorded", a 404 that
  means "already gone"). The only broad catches are the documented boundaries: the publisher's send, the resender, the
  Functions trigger.

### 10. State and concurrency

- Every write is safe to repeat: adds take a client id, actions are recorded in the ledger first, deletes ignore a
  missing row.
- Concurrency is ETags and table batches, never locks or in-memory state.
- No shared mutable flags for one-time setup (`volatile bool` check-then-set): `volatile` gives visibility, not
  atomicity. Make the call idempotent, or use `Lazy<T>` or a `SemaphoreSlim`.

### Reference examples

Copy these when adding the same kind of thing: `ChildService` (service), `ChildRepository` (repository with its own
storage rules), `FamilyRepository` (multi-table commit), `WorkspaceStore` (Blob store), `ChildrenController`
(controller), `ChildAddModel` and `ChildModel` (API models), `CreateWorkspaceHandler` (handler), `ChildValidator`
(validator), `StorageOptions` (options), `RepositoryModule` (DI module).

## C#

- Write less code. Use a small, safe, well-known package before hand-writing boilerplate.
- A fixed set of values is an enum, not a static class of string constants. A static class is only for constants and
  pure framework extension methods, never for logic: logic sits behind an injected interface.
- Keep classes short and single-purpose.
- Comments: only a rare one-line why (a security property, a platform quirk, a decision). No banner comments, no XML
  summaries that restate the code.
- Do not add checks that the platform already enforces (for example, what JwtBearer validates).

## Tests

- Tests live under `tests/<app>/`: `Lantern.Api.Tests` and `Lantern.Functions.Tests` for unit and in-process tests,
  `Lantern.Api.Test.Integration` for E2E, and `Lantern.Api.Test.Integration.Host` for the local Docker host and its
  stand-ins. Reach internals with `InternalsVisibleTo`.
- Storage behaviour is tested against real Azurite. Fakes are allowed only at the external seams: `IServiceBusService`
  (`RecordingServiceBus`) and `IKeyVaultClient` (a local RSA key). In-process tests deliver the messages the API sent to
  the real dispatcher from `Lantern.Functions`.
- Every service override, repository override and handler has tests, and every wire name (action type, problem code)
  has a test that pins it.
- **A test helper that needs a new production member is a design smell.** Write the rows or call the internals from the
  helper instead.
- **Plug local code in from outside.** Run the API through `WebApplicationFactory` with `ConfigureTestServices`, which
  applies after the API's own registrations, so the production image never contains the stand-in. A plain ASP.NET
  hosting startup does not work: it registers before the API, so the API's services win.
- **Local and UAT run the same code.** Anything the Docker stack fakes (Auth Emulator, key file, seeded Family) is
  configured from outside; there is no `if (local)` branch and no `Development` branch in `Program.cs`.

## Security and endpoints

Every new endpoint follows these. `/security-review` checks them before the PR (step 6 of `docs/agents/workflow.md`).

- **No cross-family reads.** Every read and write is scoped to the caller's own Family, taken from the caller's identity and never from a request field. A caller who sends another Family's id gets nothing back (404, not 403). Never build a query, path or filter by concatenating request text; use typed parameters and the repository's fixed keys.
- **Stateless and idempotent.** No per-instance in-memory state, because the app can run more than one replica. A repeated call is safe, as creating a Workspace folder twice is.
- **Bounded.** Set a maximum body size, maximum lengths and counts on every input, and page every list response.
- **Rate limited per caller**, keyed by the caller's uid hash, never by raw uid or IP alone.
- **Cancellable.** Every I/O call takes the `CancellationToken`. No unbounded waits.
- **Crypto ships with its tests.** Code that encrypts, hashes or wraps keys has tests for tamper detection, wrong key, wrong authenticated data, and isolation between Families. Never invent a primitive; use the platform's and Key Vault's.

## Changes that need more than code

An Azure role or resource the API needs lands and is confirmed before the API release that uses it. The infra workflow cannot grant roles, so the Owner runs the command by hand; the deploy preflight only catches storage roles. Name the command in the PR.

A shell script run by CI or Docker is committed `100755`. Editing on the Windows-mounted drive drops the mode; `git ls-files -s` shows it, and CI checks `deploy/` and `infra/`.

A change to the API, infra or data layout updates, in the same PR: the root `README.md` (Status and runtime sections), the decision log, the ADR it touches, `GLOSSARY.md` for any new or renamed domain word, and any README or skill that describes it.

## Direct Anthropic API calls

One attempt per call with no retries, a hard budget ceiling checked before each call, the cheapest adequate model, minimal targeted prompts, and a guard so an empty reply never overwrites existing content. See `docs/agents/workflow.md`.
