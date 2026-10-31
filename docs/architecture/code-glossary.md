# Code glossary

Every kind of class in Lantern's .NET code, named by its **suffix**. The suffix is a contract: it says what the class
does, where it lives, what it may depend on, and how it is registered. Pick the suffix first; the folder, base class and
registration follow from it. The meanings follow the layered architecture of [ADR-0005](../adr/0005-layered-api-generic-services.md). Domain words (Family, Child, Workspace and so on) are in [`GLOSSARY.md`](../../GLOSSARY.md);
the steps for each kind of change are in [`recipes.md`](recipes.md).

The architecture tests in `tests/lantern-api/Lantern.Api.Tests/Architecture` enforce the naming and placement rules that
are marked **(tested)**.

## Request path

The order a request travels, and the one place each concern lives:

```text
HTTP ─▶ Controller ─▶ (Interactor) ─▶ Service ─▶ Repository / Store ─▶ UnitOfWork ─▶ Table / Blob
          │ maps Model ⇄ service model      │ records Action ─▶ Publisher ─▶ RabbitMqPublishingService ─▶ exchange
          ▼                                  ▼
   ProblemExceptionHandler            Validator, Normalizer
                                                       queue ─▶ {Verb}{Noun}Function ─▶ Repository / Store
```

## API layer: `apps/lantern-api/Lantern.Api`

**Controller** (`{Nouns}Controller`, `Controllers/V{n}/`) **(tested)**
- Meaning: the HTTP surface of one resource. It binds an API model, maps it to a service model, calls one service (or
  interactor) method, maps the result back and picks the status code.
- Base: `ResourceControllerBase<TModel, TServiceModel>`.
- Never: business rules, `try`/`catch`, storage, a Family id read from the request.
- Example: `ChildrenController`. A resource the caller has exactly one of is singular: `FamilyController`.
- A read-only view with no service model of its own (`SubjectsController`) derives `ControllerBase` directly with the same
  attributes (`[ApiController]`, `[Authorize]`, `[ApiVersion]`, the rate limit) and maps through `IMapper`. It calls its own service.

**Model** (`Models/`) **(tested: only in `Lantern.Api.Models`)**
- Meaning: the shape the app sees or sends. Every API model lives here and nowhere else.
- Variants: `{Noun}Model` is a response; `{Noun}AddModel`, `{Noun}UpdateModel` and `{Noun}SaveModel` are inputs (add with a
  client id, edit, nested save); `{Noun}{Verb}Request` is a command that is not a resource (`FamilyRegisterRequest`).
- Holds DataAnnotations for shape (`[Required]`, `[StringLength]`, `[RegularExpression]` with `TextPatterns`), never rules
  that need other data.
- Example: `ChildAddModel`, `ChildModel`.

**Profile** (`ApiProfile`, `Mapping/`)
- Meaning: the one Mapster `IRegister` for API model ⇄ service model. Only a mapping that is not by name goes here.
  `*Profile` names only these classes.

**Resolver** (`HttpIdentityResolver`, `Auth/`)
- Meaning: resolves a value from the ambient context, here the caller from the token, behind a Core interface
  (`IIdentityResolver`).

**ExceptionHandler** (`ProblemExceptionHandler`, `Exceptions/`)
- Meaning: the one place a `LanternException` becomes an HTTP problem code. A new
  `LanternErrorCode` adds one line here.

**Options** (`{Section}Options`, `Configuration/`) **(tested)**: see the Core layer. API-only settings
(`FirebaseOptions`, `RateLimitOptions`) live in `Lantern.Api/Configuration`.

**Log** (`Log`, `Logging/`): one `static partial` `[LoggerMessage]` class per project. It logs ids and counts only.

## Function layer: `apps/lantern-api/Lantern.Functions`

**Function** (`{Verb}{Noun}Function`) **(tested: one per `ActionType`, named after it)**
- Meaning: one subscriber, and its business logic. It reads its own queue (`%Actions:{Verb}{Noun}Queue%`) through a
  `[RabbitMQTrigger]` method that calls `ExecuteAsync(body)`, and finishes the action in `HandleAsync(payload)`, with steps
  that are safe to repeat. The broker, not code, decides which Function gets a message. There is no dispatcher and no
  separate handler class (D123).
- Base: `ActionFunction<{Verb}{Noun}Payload>`; the base decodes the body, skips an action whose ledger row is gone and
  completes the row after `HandleAsync`. A body that is not an action throws, so the broker parks it.
- Calls repositories and stores (or services); never the ledger, never HTTP. The trigger attribute is the only broker
  type, and an architecture test confines it to `*Function` classes.
- Registered in `FunctionsModule` with `TryAddScoped` so the in-process tests can resolve it.
- Example: `RemoveWorkspaceFunction`, `CreateWorkspaceFunction`.

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
- A read-only view over a Child's own storage has no repository and so no `ServiceBase`: `SubjectService` looks the Child up
  through `IServiceBase<Child>` (which scopes it to the caller's Family) and reads through `IWorkspaceStore`. It also serves the
  Chapters of a Subject (`ChaptersAsync`, read from the Child's own Class manifest once the Subject is in the Family manifest),
  so a Child's Subjects and Chapters share one guard and one service. It also reads a Chapter's progress and history
  (`IProgressRepository`, no other service owns it yet) for the Chapters list and the Chapter history.

**Interactor** (`I{UseCase}Interactor` / `{UseCase}Interactor`, `Lantern.Base/Interactors/`)
- Meaning: one use case that orchestrates several services, when putting it in one service would make that service
  depend on its peers or grow past one responsibility, for example one use case over several services, called by its
  controller.
- Depends on services (and resolvers), never on repositories directly.
- Lantern has none yet; see [`recipes.md`](recipes.md#interactor) for when to add the first.
- Not this: validation strategies are named `*Validator`, never `*Interactor`.

**Validator** (`{Noun}Validator : IValidator<{Noun}>`, `Validation/`) **(tested)**
- Meaning: checks one service model against the product rules and throws a `LanternException` (`InvalidRequest`, or the specific code) on the first failure.
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
- A read-only view that is never a stored row (`Subject`, `Chapter`, `Page<T>`) is a sealed class with get-only properties and does not implement
  `IFamilyModel`; the manifests that carry it are `FamilyManifest` and `ClassManifest`, both deriving `Manifest` (the schema check), and
  made of `ClassSubject` (a Subject with its Chapters, as the build writes it) and `FamilyChild` (one Child's entry: Class,
  profile and Subjects, which Mapster maps from a `ClassManifest`) (ADR-0014). `ContentVersion` is `version.json`, the
  readiness marker (ADR-0013).

**Base** (`ServiceBase<T>`, `IServiceBase<T>`, `IRepositoryBase<T>`)
- Meaning: the generic behaviour every resource gets for free. `*Base` marks a class meant only to
  be derived or a contract every type of its kind shares.

**Repository contract** (`I{Noun}Repository`, `Repository/`)
- Meaning: the storage operations a service may ask for, beyond `IRepositoryBase<T>`. The contract lives in Core; the
  class lives in `Lantern.Repository`.

**Store contract** (`I{Noun}Store`, `Repository/`): the same for Blob storage (`IWorkspaceStore`).

**Calculator** (`I{Noun}Calculator` / `{Noun}Calculator`, `Service/`)
- Meaning: pure computation that more than one service shares, behind an interface. No storage, no
  state, no I/O. It is how shared logic stays out of static helpers (CODING_STANDARDS.md).
- Example: `PageCalculator` cuts a list into a page and issues and reads its cursor; `SubjectService` uses it for Subjects and
  Chapters. Registered as a singleton in `CoreModule`.

**Json service** (`IJsonService` / `JsonService`, `Service/`) **(tested: the only code that calls the JSON serializer)**
- Meaning: the one place that turns JSON text into a class and back, with one set of options (camelCase, any case on read,
  compact; `SerializeIndented` for files people read). Null or empty text reads as null; bad JSON throws. Registered as a
  singleton in `CoreModule`.
- JSON is data: read it into the class that has its shape, change that object, and write it back (`FamilyManifest.Add`,
  `Remove`). Never walk a `JsonNode` to copy values into a class. A document with no class of its own (the Board's catalog,
  which is copied as the publisher wrote it) is read as a `JsonArray` through the same service.
- Example: `WorkspaceStore` reads and writes the manifests and `version.json` through it; the publisher, the publishing
  service and the Functions use it for the message.

**Layout** (`ContentLayout`, `Lantern.Core/Storage/`) **(tested: pinned; the only code that names Workspace files and folders)**
- Meaning: every file and folder name of the Workspace and the Board masters (`class.json`, `version.json`, `refined/`,
  `summaries/`, `manifest/class-N.json` and the rest), every path built from them, and the two manifest schema ids. They are
  a contract with the content in the Board containers and with what is already stored, so they are not settings (D36) and they live in one class
  instead of as literals in the store.
- An instance registered as a singleton in `CoreModule`, injected into `WorkspaceStore`; a model that needs a schema id is
  handed it (`EnsureValid(layout.ClassManifestSchema)`). Tests that pin the real names write them as literals on purpose.
- Not for environment values (those are Options) and not for fixed sets of values (those are enums).

**Ledger** (`IActionLedger`): the record of accepted events no Function has finished.

**Action types** (`Actions/`)
- `ActionType`: the enum of events, each with a kebab-case wire name (`create-workspace`). **(tested: pinned)**
- `{Verb}{Noun}Payload`: the record an event carries. **(tested: one per `ActionType`)**
- `ActionMessage`: the envelope (id, type, JSON payload, trace parent). Its type is the routing key.
- `IActionPublisher` / `ActionPublisher`: `RecordAsync` writes the ledger row (the commit point); `SendAsync` publishes it to
  the exchange under the wire name and logs, never throws, on failure.

**Generic external service** (`I{Resource}Service` / `{Resource}Service`, or `I{Resource}Client`)
- Meaning: the one gateway to an external resource, knowing nothing of what it carries. `RabbitMqPublishingService` publishes any model to any exchange under a key;
  `CryptoService` holds the uid hash (Lantern holds no Family key, ADR-0010). **(tested: each Azure client has one owner)**

**Resolver contract** (`IIdentityResolver`): the caller, resolved per request by the host.

**Exception** (`LanternException`, `Exceptions/`)
- Meaning: the one exception for a failure the caller must see. A `LanternErrorCode` (in `Constants/`) says which
  condition; `ProblemExceptionHandler` maps each code to a status and a problem code. Name the condition
  (`ChildLimitReached`), not the status code. A new condition is a new `LanternErrorCode` member and one line in the
  handler, never a new exception class.

**Options** (`{Section}Options` with `const string SectionName`, `Configuration/`) **(tested: no defaults)**
- Meaning: one configuration section, bound and validated with DataAnnotations; the host calls `ValidateOnStart`.
  Values come from the bicepparam ([D36](../ideation/decision-log.md)).

**Module** (`{Layer}Module.AddLantern{Layer}()`): the one DI registration per project. `CoreModule`, `RepositoryModule`, `BaseModule`, `FunctionsModule`.

**Enum** (`{Noun}Type`, `{Noun}Status` or `{Noun}Code`, `Constants/`, namespace `Lantern.Core.Constants`): a fixed set of
values. `ActionType`, `ChildStatus`, `BoardType`, `LanternErrorCode`. Every enum is a
constant, so every enum lives in `Lantern.Core.Constants`. Never a static class of string constants.

**Manifest** (`{Noun}Manifest`, `Models/`)
- Meaning: a derived, versioned JSON document that is read back as a model: `ClassManifest` (the Subjects and Chapters of one
  Board and Class, made by the build and copied into each slice as `content/manifest.json`) and `FamilyManifest` (the index
  of the Children whose slice is ready, kept in `{familyId}/manifest.json`). Both check their `schema` and refuse one they do
  not know. A change to a manifest returns a new one, so a retry after a lost race starts again from what is stored (ADR-0014).
- Never the record of readiness (that is `content/version.json`) and never a list of blobs (that is a Snapshot).

## Repository layer: `libs/Lantern.Repository` (storage only)

**Repository** (`{Noun}Repository`, the root folder) **(tested: named after its model and entity)**
- Meaning: stores one service model. Maps it to its entity and back (locked values pass through untouched), and applies the storage rules
  that need atomicity (ETags, batches).
- Base: `BaseRepository<{Noun}, {Noun}Entity>`; supply `RowKeyPrefix`, `RowKey(id)` and `IdOf(model)`, and override
  only operations whose storage rules differ.
- Registered scoped in `RepositoryModule`, plus a forward from `IRepositoryBase<{Noun}>`.
- Example: `ChildRepository` (the limit of six under the Family row's ETag), `FamilyRepository` (a multi-table commit).

**Entity** (`{Noun}Entity`, `Entities/`) **(tested: internal)**
- Meaning: the stored row shape. Derives `TableEntityBase`; personal text is a `{Field}Locked` string the phone locked. Internal, never returned.

**UnitOfWork** (`UnitOfWork<TEntity>`, `UnitOfWork/`) **(tested: the only `TableClient` holder)**
- Meaning: the only class that sends table requests. One per entity type,
  registered in `RepositoryModule`.

**Store** (`{Noun}Store`) **(tested: the only Blob client holder)**
- Meaning: the Blob counterpart of a repository. `WorkspaceStore` owns the Workspace paths and marker.

**Snapshot** (`{Noun}Snapshot`, a private record inside the Store that reads a master container)
- Meaning: the blobs one copy will make, each with its ETag, read from the master before the copy and read again after it.
  A second read that differs means the master changed mid-copy, so the slice is not marked ready (ADR-0013, D90).
- Examples: `ContentSnapshot` (one Class's slice), `BookSnapshot` (one Book's files).
- Never: a "plan". Plan is the Parent's intent to study a Chapter, the first step of Plan, Ask and Check (`GLOSSARY.md`), so
  no class, method or comment names a copy's file list that. Not a Manifest either: these were called `ContentManifest` and
  `BookManifest` until ADR-0014 gave that word to the derived documents in Core.

**Ledger** (`ActionLedger`): the action ledger's table operations.

**Progress repository** (`ProgressRepository`, no base class): the `progress` table's operations, for the rows that are not one per id: a Chapter's summary, Attempt and Check rows (ADR-0016). Like `ActionLedger` it is purpose-built, and it is the only holder of those three entities. Each change is one table transaction in the Family partition, retried when a race is lost.

**RowKeyService** (`IRowKeyService`): every partition and row key. Keys are cipher context, so they are fixed in code.

**Profile** (`RepositoryProfile`): the one Mapster `IRegister` for service model ⇄ entity. Only what is not by name goes
here (the Board, stored as text; a row with no Board reads as CBSE).

**Attribute** (`{Meaning}Attribute`)
- Meaning: declarative metadata a framework class reads by reflection, so the rule sits on the thing it describes
  (for example a role check on endpoints, or translation and audit markers on models). Lantern has none
  today. A new attribute lives beside the code that reads it.

## Kinds Lantern does not use

These are decided, not missing. Use the Lantern alternative.

| Kind | What it would do | Lantern instead |
|---|---|---|
| `*Filter : BaseDataFilter<TEntity>` | An implicit tenant filter on every SQL query | The Family partition key: every read is already one partition |
| `*Security : ServiceAccessControlBase<T>` | Per-model access checks before a save or remove | Family scope in `ServiceBase`. Add a `{Noun}Security` only when a rule goes beyond "same Family" (for example, Parent roles) |
| `*Job` (Quartz) | Runs scheduled work | An event and a Function; no timers (D31) |
| `*Middleware` | Handles HTTP pipeline concerns | ASP.NET's built-ins plus `ProblemExceptionHandler` |
| `*Context` (EF `DbContext`) | Holds a SQL session | `UnitOfWork<TEntity>` |
| `*Helper`, `*Utils` | Collects static helpers | An injected interface (CODING_STANDARDS.md: inversion of control) |

When another conventional kind becomes necessary (`*Transformer`, `*Generator`, `*Converter`, `*Result`, `*Factory`),
introduce it behind an interface, in the layer whose concern it is, and add it to this glossary in the same PR.
