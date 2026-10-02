# Code glossary

Every kind of class in Lantern's .NET code, named by its **suffix**. The suffix is a contract: it says what the class
does, where it lives, what it may depend on, and how it is registered. Pick the suffix first; the folder, base class and
registration follow from it. The meanings come from the user's `wf` architecture (`E:\wf\wf`) and keep wf's context; where Lantern
differs, the entry says so. Domain words (Family, Child, Workspace and so on) are in [`GLOSSARY.md`](../../GLOSSARY.md);
the steps for each kind of change are in [`recipes.md`](recipes.md).

The architecture tests in `tests/lantern-api/Lantern.Api.Tests/Architecture` enforce the naming and placement rules that
are marked **(tested)**.

## Request path

The order a request travels, and the one place each concern lives:

```text
HTTP ─▶ Controller ─▶ (Interactor) ─▶ Service ─▶ Repository / Store ─▶ UnitOfWork ─▶ Table / Blob
          │ maps Model ⇄ service model      │ records Action ─▶ Publisher ─▶ ServiceBusService ─▶ queue
          ▼                                  ▼
   ProblemExceptionHandler            Validator, Normalizer
                                                       queue ─▶ ActionFunction ─▶ Dispatcher ─▶ Handler ─▶ Repository / Store
```

## API layer: `apps/lantern-api/Lantern.Api`

**Controller** (`{Nouns}Controller`, `Controllers/V{n}/`) **(tested)**
- Meaning: the HTTP surface of one resource. It binds an API model, maps it to a service model, calls one service (or
  interactor) method, maps the result back and picks the status code.
- Base: `ResourceControllerBase<TModel, TServiceModel>` (wf: `ResourceControllerBase<TSaveModel, TModel, TServiceModel>`).
- Never: business rules, `try`/`catch`, storage, a Family id read from the request.
- Example: `ChildrenController`.

**Model** (`Models/`) **(tested: only in `Lantern.Api.Models`)**
- Meaning: the shape the app sees or sends. Every API model lives here and nowhere else.
- Variants: `{Noun}Model` is a response; `{Noun}AddModel`, `{Noun}UpdateModel` and `{Noun}SaveModel` are inputs (add with a
  client id, edit, nested save); `{Noun}{Verb}Request` is a command that is not a resource (`FamilyRegisterRequest`).
- Holds DataAnnotations for shape (`[Required]`, `[StringLength]`, `[RegularExpression]` with `TextPatterns`), never rules
  that need other data.
- Example: `ChildAddModel`, `ChildModel`.

**Profile** (`ApiProfile`, `Mapping/`)
- Meaning: the one Mapster `IRegister` for API model ⇄ service model. Only a mapping that is not by name goes here.
  `*Profile` names only these classes (wf: `ApiProfile`, `CoreApiMapperProfile`).

**Resolver** (`HttpIdentityResolver`, `Auth/`)
- Meaning: resolves a value from the ambient context, here the caller from the token, behind a Core interface
  (`IIdentityResolver`). wf uses Resolver both this way (`IdentityResolver`) and for per-type strategies
  (`ResponseExportResolver`).

**ExceptionHandler** (`ProblemExceptionHandler`, `Exceptions/`)
- Meaning: the one place a Core exception becomes an HTTP problem code (wf: `ExceptionMiddleware`). A new caller-facing
  exception adds one line here.

**Options** (`{Section}Options`, `Configuration/`) **(tested)**: see the Core layer. API-only settings
(`FirebaseOptions`, `RateLimitOptions`) live in `Lantern.Api/Configuration`.

**Log** (`Log`, `Logging/`): one `static partial` `[LoggerMessage]` class per project. It logs ids and counts only.

## Function layer: `apps/lantern-api/Lantern.Functions`

**Function** (`ActionFunction`)
- Meaning: an Azure Functions entry point (wf: `*Trigger`). Lantern has one: it reads `workspace-events`, hands each message
  to the dispatcher and settles it. A new event never adds a Function.

**Dispatcher** (`ActionDispatcher`, `Handler/`)
- Meaning: picks the one handler for an `ActionType`. Never changed for a new event.

**Handler** (`{Verb}{Noun}Handler`, `Handler/`) **(tested: one per `ActionType`, named after it)**
- Meaning: finishes one event type, with steps that are safe to repeat.
- Base: `ActionHandler<{Verb}{Noun}Payload>`; the base checks and completes the ledger.
- Calls repositories and stores (or services); never the ledger, never HTTP.
- Registered in `FunctionsModule` with `TryAddEnumerable`.
- Example: `RemoveWorkspaceHandler`, `CreateWorkspaceHandler`.

## Service layer: `libs/Lantern.Base`

**Service** (`I{Noun}Service` / `{Noun}Service`, `Services/`) **(tested)**
- Meaning: the business operations on one service model, always inside the caller's Family. It owns validation,
  normalization and the order of storage calls and event recording.
- Base: `ServiceBase<{Noun}, I{Noun}Repository>`; the interface derives `IServiceBase<{Noun}>`. Override only what
  differs; use the base's `Repository`, `Families`, `Identity` and `FamilyIdAsync`.
- A model with no logic beyond Family scoping has **no** service class: the open generic `IServiceBase<T>` serves it.
- Never: Azure SDK types, row keys, ciphertext, HTTP types.
- Registered scoped in `BaseModule`.
- Example: `ChildService`, `FamilyService`.

**Interactor** (`I{UseCase}Interactor` / `{UseCase}Interactor`, `Lantern.Base/Interactors/`)
- Meaning: one use case that orchestrates several services, when putting it in one service would make that service
  depend on its peers or grow past one responsibility. In wf, `ReportInteractor` sits over the Request, Response,
  Accessor, Notification and Inquiry services and is called by `ReportController`.
- Depends on services (and resolvers), never on repositories directly.
- Lantern has none yet; see [`recipes.md`](recipes.md#interactor) for when to add the first.
- Not this: wf also calls its validation strategies `*Interactor` (`EmailValidator : IValidationInteractor`); Lantern
  names those `*Validator`.

**Validator** (`{Noun}Validator : IValidator<{Noun}>`, `Validation/`) **(tested)**
- Meaning: checks one service model against the product rules and throws `InvalidRequestException` on the first failure.
  One validator per model; a composite model's validator calls its parts' validators (`RegistrationValidator` uses the
  Child rules).
- Registered as a singleton `IValidator<T>` in `BaseModule`.

**Normalizer** (`I{Noun}TextNormalizer` / `{Noun}TextNormalizer`, `Validation/`)
- Meaning: puts input into its stored form (trim, collapse whitespace, empty to null) before validation and storage.
- Example: `ChildTextNormalizer`.

## Core: `libs/Lantern.Core` (framework and contracts)

**Service model** (`{Noun}`, `Models/`)
- Meaning: what services and repositories exchange. Plaintext, no storage detail. Implements `IFamilyModel` when it
  belongs to a Family. Examples: `Child`, `Family`, `Parent`, `Registration`.

**Base** (`ServiceBase<T>`, `IServiceBase<T>`, `IRepositoryBase<T>`)
- Meaning: the generic behaviour every resource gets for free (wf: the same names). `*Base` marks a class meant only to
  be derived or a contract every type of its kind shares.

**Repository contract** (`I{Noun}Repository`, `Repository/`)
- Meaning: the storage operations a service may ask for, beyond `IRepositoryBase<T>`. The contract lives in Core; the
  class lives in `Lantern.Repository`.

**Store contract** (`I{Noun}Store`, `Repository/`): the same for Blob storage (`IWorkspaceStore`).

**Ledger** (`IActionLedger`): the record of accepted events no handler has finished.

**Action types** (`Actions/`)
- `ActionType`: the enum of events, each with a kebab-case wire name (`create-workspace`). **(tested: pinned)**
- `{Verb}{Noun}Payload`: the record an event carries. **(tested: one per `ActionType`)**
- `ActionMessage`: the envelope (id, type, JSON payload).
- `IActionPublisher` / `ActionPublisher`: `RecordAsync` writes the ledger row (the commit point); `SendAsync` puts it on
  the queue and logs, never throws, on failure.

**Generic external service** (`I{Resource}Service` / `{Resource}Service`, or `I{Resource}Client`)
- Meaning: the one gateway to an external resource, knowing nothing of what it carries (wf: `IBusService<T>`, the
  `*Client` libraries). `ServiceBusService` sends any model to any queue; `KeyVaultClient` wraps and unwraps;
  `CryptoService` holds the AES-GCM and HMAC code. **(tested: each Azure client has one owner)**

**Resolver contract** (`IIdentityResolver`): the caller, resolved per request by the host.

**Exception** (`{Condition}Exception`, `Exceptions/`) **(tested: one per file)**
- Meaning: a failure the caller must see. Each maps to one problem code in `ProblemExceptionHandler`. Name the condition
  (`ChildLimitReachedException`), not the status code.

**Options** (`{Section}Options` with `const string SectionName`, `Configuration/`) **(tested: no defaults)**
- Meaning: one configuration section, bound and validated with DataAnnotations; the host calls `ValidateOnStart`.
  Values come from the bicepparam ([D36](../ideation/decision-log.md)). wf calls these `*Configuration`.

**Module** (`{Layer}Module.AddLantern{Layer}()`): the one DI registration per project (wf: `*Module`,
`DependencyInjection`). `CoreModule`, `RepositoryModule`, `BaseModule`, `FunctionsModule`.

**Enum** (`{Noun}Type` or `{Noun}Status`): a fixed set of values (wf: `ActivationType`, `*Status`). `ActionType`,
`ChildStatus`. Never a static class of string constants.

## Repository layer: `libs/Lantern.Repository` (storage only)

**Repository** (`{Noun}Repository`, the root folder) **(tested: named after its model and entity)**
- Meaning: stores one service model. Maps it to its entity and back, encrypts and decrypts, and applies the storage rules
  that need atomicity (ETags, batches).
- Base: `BaseRepository<{Noun}, {Noun}Entity>`; supply `RowKeyPrefix`, `RowKey(id)` and `IdOf(model)`, and override
  only operations whose storage rules differ.
- Registered scoped in `RepositoryModule`, plus a forward from `IRepositoryBase<{Noun}>`.
- Example: `ChildRepository` (the limit of six under the Family row's ETag), `FamilyRepository` (a multi-table commit).

**Entity** (`{Noun}Entity`, `Entities/`) **(tested: internal)**
- Meaning: the stored row shape. Derives `TableEntityBase`; encrypted columns are marked `[Encrypted("column")]` and named
  `{Field}Cipher`. Internal, never returned.

**UnitOfWork** (`UnitOfWork<TEntity>`, `UnitOfWork/`) **(tested: the only `TableClient` holder)**
- Meaning: the only class that sends table requests (wf: `UnitOfWork`, `GenericUnitOfWork`). One per entity type,
  registered in `RepositoryModule`.

**Store** (`{Noun}Store`) **(tested: the only Blob client holder)**
- Meaning: the Blob counterpart of a repository. `WorkspaceStore` owns the Workspace paths and marker.

**Ledger** (`ActionLedger`): the action ledger's table operations.

**RowKeyService** (`IRowKeyService`): every partition and row key. Keys are cipher context, so they are fixed in code.

**Protector, KeyRing** (`Security/`): `FieldProtector` encrypts `[Encrypted]` columns with the row's keys as authenticated
data; `FamilyKeyRing` unwraps each Family key at most once per request.

**Profile** (`RepositoryProfile`): the one Mapster `IRegister` for service model ⇄ entity (the `*Cipher` renames).

**Attribute** (`{Meaning}Attribute`)
- Meaning: declarative metadata a framework class reads by reflection, so the rule sits on the thing it describes
  (wf: `RoleAccessAttribute` on endpoints, `TranslationAttribute` and `AuditAttribute` on models). Lantern:
  `EncryptedAttribute` on entity columns. A new attribute lives beside the code that reads it.

## Kinds wf has that Lantern does not use

These are decided, not missing. Use the Lantern alternative.

| wf kind | What it does in wf | Lantern instead |
|---|---|---|
| `*Filter : BaseDataFilter<TEntity>` | Implicit tenant filter on every SQL query | The Family partition key: every read is already one partition |
| `*Security : ServiceAccessControlBase<T>` | Per-model access checks before save or remove | Family scope in `ServiceBase`. Add a `{Noun}Security` only when a rule goes beyond "same Family" (for example, Parent roles) |
| `*Job` (Quartz) | Scheduled work | An event and a handler; no timers (D31) |
| `*Middleware` | HTTP pipeline concerns | ASP.NET's built-ins plus `ProblemExceptionHandler` |
| `*Context` (EF `DbContext`) | SQL session | `UnitOfWork<TEntity>` |
| `*Helper`, `*Utils` | Static grab-bags | An injected interface (CODING_STANDARDS.md: inversion of control) |

When one of the remaining wf kinds becomes necessary (`*Transformer` converts a set of models into another shape,
`*Generator` produces a file, `*Calculator` does pure computation, `*Converter` converts one value, `*Result` is an
operation's multi-part return, `*Factory` builds objects DI cannot), introduce it with wf's meaning, behind an
interface, in the layer whose concern it is, and add it to this glossary in the same PR.
