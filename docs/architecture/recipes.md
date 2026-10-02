# Recipes

The steps for each kind of backend change. Find the change in the decision list, follow its recipe in order, and finish
with **Done**. Every suffix used here is defined in [`code-glossary.md`](code-glossary.md); copy the named reference
class rather than inventing a shape.

## Decision list

1. A new verb on a resource that already exists (for example, move a Child to a new Class)? **[Endpoint on an existing resource](#endpoint-on-an-existing-resource)**.
2. A new kind of thing a Family owns, with its own id? **[New resource](#new-resource)**.
3. Work that is slow, must not fail halfway, or must outlive the request? **[New event](#new-event)**.
4. One use case needs two or more services in a fixed order? **[Interactor](#interactor)**.
5. A new input rule? **[Validation](#validation)**.
6. A new setting? **[Setting](#setting)**.
7. A new external system? **[External dependency](#external-dependency)**.

A change can need several: moving a Child to a new Class is recipe 1, with recipe 3 for its Workspace folder.

## Reuse or create

Reuse is the default. Create a class only when the reuse case fails.

| You need | Reuse when | Create when |
|---|---|---|
| Service | The model has no rule beyond Family scoping: inject `IServiceBase<{Noun}>` | The model has its own rules: `{Noun}Service : ServiceBase<{Noun}, I{Noun}Repository>` |
| Service method | An existing operation (`AddAsync`, `UpdateAsync`, `RemoveAsync`, `SingleAsync`, `CollectionAsync`) fits once you override it | The verb is genuinely new (`AddOrGetAsync`, `RegisterAsync`): add it to `I{Noun}Service` |
| Repository | The model is stored as one row per id in the Family partition: `BaseRepository` with three members | Never skip it: every stored model has its own `{Noun}Repository`, even when it overrides nothing |
| Repository method | The base's single, collection, add, update or remove fits | Storage needs a rule the base lacks (ETag retry, a batch with another row, a status-only read): add to `I{Noun}Repository` |
| Store | Blob data belongs to the Workspace: add a method to `IWorkspaceStore` | A new Blob area with its own paths: `{Noun}Store` |
| Event | Never reuse a type for a different meaning | Each distinct outcome gets its own `ActionType` |
| Exception | An existing condition fits exactly (`NotFoundException("child")`) | A new condition the caller must tell apart: a new exception and a new problem code |
| Interactor | One service can do it with its own repository | Two or more services in a fixed order, see below |

A service never injects another service's repository. When a service needs another model's data, it injects that
model's `IServiceBase<T>` or `I{Noun}Service`, or it is time for an interactor.

## Endpoint on an existing resource

1. **Contract.** If the verb is new, add the method to `I{Noun}Service` (Base) and implement it in `{Noun}Service`.
   Otherwise override the base operation. Reference: `ChildService.AddOrGetAsync`, `ChildService.UpdateAsync`.
2. **Rules.** Validate with the model's `IValidator<T>` and normalize with its normalizer. A caller-facing failure throws
   a Core exception.
3. **Storage.** If the base repository operation does not fit, add the method to `I{Noun}Repository` (Core) and
   implement it in `{Noun}Repository`. Reference: `ChildRepository.MarkDeletingAsync`.
4. **API model.** Add the input model to `Lantern.Api/Models` (`{Noun}UpdateModel`, or `{Noun}{Verb}Request` for a
   command). Map anything not by name in `ApiProfile`.
5. **Action.** Add the method to the existing `{Nouns}Controller`: bind, `Mapper.Map`, call one service method, return
   the status code (`Ok`, `Created`, `Accepted` for queued work).
6. **Errors.** A new exception gets one line in `ProblemExceptionHandler` with its problem code.
7. **Tests.** An in-process controller test in `Lantern.Api.Tests/Controllers` (happy path, another Family's id → 404,
   bad input → 400), and an Azurite repository test for any new storage rule.

## New resource

1. **Service model.** `Lantern.Core/Models/{Noun}.cs`, implementing `IFamilyModel`, plaintext, no storage fields.
2. **Entity.** `Lantern.Repository/Entities/{Noun}Entity.cs`, `internal sealed`, deriving `TableEntityBase`. Mark
   personal text `[Encrypted("column")]` on a `{Field}Cipher` property, and add the renames to `RepositoryProfile`.
3. **Keys.** Add `{Noun}RowPrefix` and `{Noun}RowKey(id)` to `IRowKeyService` and `RowKeyService`. They are cipher
   context: once released, never changed.
4. **Repository.** `I{Noun}Repository : IRepositoryBase<{Noun}>` in `Core/Repository`, and
   `{Noun}Repository : BaseRepository<{Noun}, {Noun}Entity>, I{Noun}Repository` supplying `RowKeyPrefix`, `RowKey` and
   `IdOf`. Register it scoped in `RepositoryModule` with the `IRepositoryBase<{Noun}>` forward and an
   `AddUnitOfWork<{Noun}Entity>` line naming its table option.
5. **Service.** None if the generic one suffices (see Reuse or create); otherwise the Service row above, registered
   scoped in `BaseModule`.
6. **API.** `{Noun}Model` and its input models, then `{Nouns}Controller : ResourceControllerBase<{Noun}Model, {Noun}>`
   in `Controllers/V1` with `[ApiVersion]`, `[Route("v1/family/{nouns}")]`, `[EnableRateLimiting]` and
   `[RequestSizeLimit]`.
7. **Removal.** If removing it must also remove other data, it is an event (next recipe), as `RemoveWorkspace` is.
8. **Tests.** Repository tests on Azurite (including another Family's id and stored ciphertext), controller tests, and
   the architecture tests pass unchanged.

## New event

Work that runs after the request, through the one `workspace-events` queue.

1. **Type.** Add `{Verb}{Noun}` to `ActionType` with `[JsonStringEnumMemberName("{verb}-{noun}")]`. The member name is the
   ledger partition and the wire name is the queue contract: never rename either once released.
2. **Payload.** `Lantern.Core/Actions/{Verb}{Noun}Payload.cs`, a record carrying ids only (Family id from the token).
3. **Record and send.** In the service: `actions.RecordAsync(type, id, payload)` **before** the service's own writes,
   then the writes, then `actions.SendAsync(message)`. The action id makes a repeat the same row. Reference:
   `ChildService.RemoveAsync`, `ChildService.AddOrGetAsync`.
4. **Handler.** `Lantern.Functions/Handler/{Verb}{Noun}Handler.cs : ActionHandler<{Verb}{Noun}Payload>`. Every step
   is safe to repeat; check the state the API left (a status read) before acting, because the ledger row is written
   before the API's own rows. Register it in `FunctionsModule`. Reference: `CreateWorkspaceHandler`.
5. **Log.** A `[LoggerMessage]` in `FunctionLog` with a new event id.
6. **Tests.** A wire-name test, an in-process test that delivers the sent message (`factory.DeliverAsync()`), a
   redelivery or repeat test, and a failure-then-redelivery test.

Never add a queue, a Function, a timer or a `BackgroundService` for an event.

## Interactor

Add one only when a single use case orchestrates **two or more services** in a fixed order and putting it in one of them
would make that service depend on a peer. Until then, keep the logic in the service.

1. Tell the user why it is needed (which services, in what order) before writing it: it is a new layer for that use
   case.
2. `Lantern.Base/Interactors/I{UseCase}Interactor.cs` and `{UseCase}Interactor.cs`, `internal sealed`, primary
   constructor injecting the services (and `IIdentityResolver` if needed), never a repository or store.
3. Methods are named after the use case's verbs. It owns ordering and compensation, not rules: a rule stays in the
   service that owns the model.
4. Register it scoped in `BaseModule`.
5. The controller injects the interactor instead of the services (wf: `ReportController` → `ReportInteractor`).
6. Test it through its controller on Azurite, as the other in-process tests do; replace a collaborator only at the external seams.
7. Add the interactor to `code-glossary.md`'s Service layer section as the reference example.

## Validation

1. Shape rules (required, length, characters) go on the API model as DataAnnotations, with `TextPatterns`.
2. Product rules (age range, limits, consent) go in `{Noun}Validator : IValidator<{Noun}>` in `Lantern.Base/Validation`,
   registered as a singleton. A composite validator injects its parts' validators.
3. Rules that need stored data (the limit of six) belong in the service, and in the repository when they need
   atomicity.

## Setting

1. Add the property to the section's `{Section}Options` with DataAnnotations and **no default**; a new section gets a
   new class in the owning layer's `Configuration/` with `const string SectionName`.
2. Bind it in the layer's module with `ValidateDataAnnotations()`; the host that uses it calls `ValidateOnStart()`.
3. Add the value to `infra/params/lantern.uat.bicepparam`, a param in `infra/main.bicep`, and the env var
   (`{Section}__{Property}`) on each Container App that reads it.
4. Add the key with an empty value to `appsettings.json`, the local value to `appsettings.Development.json`, and to
   `deploy/local/docker-compose.yml` for the Functions container if it reads it.
5. In-process tests set it with `UseSetting` in `LanternApiFactory`.

Storage formats, wire names and product rules are never settings ([D36](../ideation/decision-log.md)).

## External dependency

1. A contract in Core (`I{Resource}Service` or `I{Resource}Client`) that knows nothing of the domain: it takes and returns
   general types (`SendAsync<T>(queue, message)`, `WrapKeyAsync(bytes)`).
2. The implementation in Core holds the SDK client, created lazily from its options; it is the only holder of that
   client type (add a row to `LayerTests.ClientOwners`).
3. Domain code wraps it in the layer that needs it (`ActionPublisher` over `ServiceBusService`).
4. Tests replace it at this seam only (`RecordingServiceBus`, a local RSA `IKeyVaultClient`).

## Done

- `dotnet build Lantern.slnx` passes with warnings as errors, and the full suite passes, including
  `Lantern.Api.Tests/Architecture`.
- The Docker E2E passes when the change touches an endpoint or an event (`deploy/local/README.md`).
- In the same PR: `GLOSSARY.md` for a new domain word, `code-glossary.md` for a new kind of class, the decision log and
  ADR for a changed decision, and the root `README.md` Status for a new endpoint.
