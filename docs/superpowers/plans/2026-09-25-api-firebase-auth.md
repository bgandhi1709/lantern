# Lantern API: Firebase JWT auth and mother registration — Implementation Plan

> **For agentic workers:** Implement inline in one session, task by task, commit after each task. Do not dispatch subagents. Steps use checkbox syntax.

**Goal:** A mother signs in with Google in the app, sends her Firebase ID token, and registers her family once: her region and language, her consent, and one to six children.

**Architecture:** One ASP.NET controller app. JwtBearer validates the Firebase ID token (signature, issuer, audience, lifetime); nothing else is hand-rolled. Azure Table storage holds one partition per mother, so `profile` and `child_*` rows are written in one atomic batch. Name, email and children's names are encrypted in code with AES-GCM.

**Tech stack:** .NET 10, ASP.NET Core controllers, `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.12, `Azure.Data.Tables` 12.13.0, `Azure.Identity` 1.21.0, xunit.v3 4.0.1, `Microsoft.AspNetCore.Mvc.Testing` 10.0.12, Azurite, GitHub Actions.

**Firebase project (UAT, the only one for now):** `lantern-ai-bg1709`. Issuer `https://securetoken.google.com/lantern-ai-bg1709`, audience `lantern-ai-bg1709`.

## Global constraints

- Same conventions as the sibling repos: `dist/` output, warnings as errors, central package management. Already in place from the scaffold commit `af7e493`.
- No `ConfigureAwait(false)`. Every log line uses source-generated `[LoggerMessage]`.
- Options validated on start (`ValidateDataAnnotations().ValidateOnStart()`).
- **Auth is JwtBearer only.** No custom token rules class, no runtime test-token mode. Google is the only provider enabled in the Firebase console, so no provider check in code.
- **Never log** the token, uid, email or name. `IncludeErrorDetails = false`. Error responses use fixed titles and never echo exception messages.
- The uid is never stored raw. Partition key is `HMAC-SHA256(uid)`.
- Tests run the real JwtBearer pipeline. Only the signing-key source is replaced with a test RSA key, inside the test project.
- GitHub Actions are pinned to commit SHAs. Images are pushed only from `main`.
- Commit messages end with `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`.

## Data model

One table, `parents`. One partition per mother: `PartitionKey = HMAC-SHA256(uid)` as lowercase hex.

| RowKey | Columns | Encrypted |
|---|---|---|
| `profile` | `FamilyId`, `NameCipher`, `EmailCipher`, `Region`, `Language`, `ConsentVersion`, `ConsentAt`, `CreatedAt` | name, email |
| `child_{guid:N}` | `NameCipher`, `SchoolCipher?`, `ClassLevel`, `BirthYear`, `CreatedAt` | name, school |

- Name and email come from the token's `name` and `email` claims. They are not in the request body. A missing claim is stored as an empty string.
- One key setting, `Security:Key` (base64, at least 32 bytes). HKDF-SHA256 derives two subkeys from it: one for the uid HMAC, one for AES-GCM.
- Cipher format: `v1.` + base64(nonce 12 bytes | tag 16 bytes | ciphertext). Fresh random nonce every time. Associated data is `PartitionKey|RowKey|column`, so a value cannot be moved to another row or column.
- Envelope encryption with a Key Vault master key stays with issue #23. This is the interim scheme.

## API

| Method | Route | Result |
|---|---|---|
| POST | `/v1/register` | **201** with `{familyId, children:[{childId, name, classLevel, birthYear, school}]}`. **409** `already-registered` if this mother has a profile. **400** on validation. |
| GET | `/v1/me` | **200** same shape plus `region`, `language`, `name`, `email`, `consentVersion`. **404** `not-registered` (the app uses this to decide whether to show onboarding). |
| GET | `/health/live` | **200**, anonymous |

Request body for register:
```json
{
  "region": "Gujarat",
  "language": "gu",
  "consent": { "accepted": true, "noticeVersion": "2026-09" },
  "children": [
    { "name": "Aarav", "classLevel": 1, "birthYear": 2020, "school": "optional" }
  ]
}
```
Validation: region 1–60 chars; language one of `en`, `gu`, `hi`; consent must be `true`; children 1–6; child name 1–40 chars, no control characters; classLevel 1–10; birthYear between (this year − 18) and (this year − 3); school at most 120 chars. Text is trimmed.

Every endpoint is closed by default (fallback policy `RequireAuthenticatedUser`). Health is `[AllowAnonymous]`. `familyId` comes only from the token, never from the route or body.

---

### Task 1: Drop the token-rules class

**Files:** delete `Lantern.Api/Auth/FirebaseTokenRules.cs` and `Lantern.Api.Tests/Auth/FirebaseTokenRulesTests.cs`. Keep the JwtBearer `PackageReference`.

- [ ] `git status` first. Discard the half-finished uncommitted edits to those two files, then remove both files.
- [ ] `dotnet build Lantern.slnx -c Release` (0 warnings) and `dotnet test --solution Lantern.slnx -c Release` (health test passes).
- [ ] Commit: `Drop custom token rules; JwtBearer validates Firebase tokens`.

### Task 2: Firebase JWT authentication scheme

**Files:** create `Configuration/FirebaseOptions.cs`, `Auth/FirebaseAuthentication.cs`, `Logging/Log.cs`, `Middleware/ProblemExceptionHandler.cs`, tests `Infrastructure/TestTokens.cs`, `Infrastructure/CapturingLoggerProvider.cs`, `Auth/AuthenticationTests.cs`. Modify `Program.cs`, `appsettings*.json`, and `LanternApiFactory`.

**Produces:** `AddFirebaseAuthentication(services, configuration)`; `TestTokens.Create(uid, projectId = "lantern-test", expires = null, key = null, name = null, email = null)`; `TestTokens.Key` (RSA 2048); the fallback policy.

```csharp
options.Authority = $"https://securetoken.google.com/{projectId}";
options.MapInboundClaims = false;
options.IncludeErrorDetails = false;
options.TokenValidationParameters = new()
{
    ValidateIssuer = true, ValidIssuer = $"https://securetoken.google.com/{projectId}",
    ValidateAudience = true, ValidAudience = projectId,
    ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(2),
    ValidateIssuerSigningKey = true,
};
options.Events = new JwtBearerEvents
{
    OnAuthenticationFailed = c => { Log.TokenInvalid(logger, c.Exception.GetType().Name); return Task.CompletedTask; },
};
```
`appsettings.Development.json` sets `Firebase:ProjectId` to `lantern-ai-bg1709` (not secret). The exception handler maps `RequestFailedException` to 503 `storage-unavailable` and everything else to 500, with fixed titles.

The test factory replaces only the key source: `PostConfigure<JwtBearerOptions>(o => o.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(...TestTokens.Key))`, with `Firebase:ProjectId = lantern-test`.

- [ ] Failing tests, using a temporary authorized test endpoint in the test project (removed in Task 4): no token → 401; a theory of wrong audience, wrong issuer, expired, and signed by another key → 401; a valid token → 200. Also: the 401 `WWW-Authenticate` header has no `error_description`, and the capturing logger sees no token text.
- [ ] Implement, run the tests, commit: `Authenticate requests with Firebase ID tokens`.

### Task 3: Storage and field encryption

**Files:** create `Configuration/{StorageOptions,SecurityOptions}.cs`, `Services/{KeyDeriver,FieldCipher}.cs`, `Repository/{ParentRepository,Entities}.cs`, tests `Services/FieldCipherTests.cs`, `Repository/ParentRepositoryTests.cs`.

**Produces:**
```csharp
public sealed class KeyDeriver { string HashUid(string uid); byte[] FieldKey { get; } }
public interface IFieldCipher { string Protect(string plaintext, string partitionKey, string rowKey, string column);
                                string Unprotect(string value, string partitionKey, string rowKey, string column); }
public interface IParentRepository {
    Task<bool> TryRegisterAsync(ParentProfile profile, IReadOnlyList<ChildRecord> children, CancellationToken ct); // false = already registered
    Task<(ParentProfile Profile, IReadOnlyList<ChildRecord> Children)?> GetAsync(string partitionKey, CancellationToken ct);
}
```
`StorageOptions`: exactly one of `ConnectionString` (Azurite) or `TableEndpoint` (production, `DefaultAzureCredential`). `SecurityOptions.Key`: base64, at least 32 bytes.

- [ ] Failing tests:
  - Cipher: round trip; a value moved to another row or column fails to decrypt; a tampered byte fails; the same text encrypts differently each time.
  - `HashUid`: stable; differs by case; is not the uid.
  - Azurite: register then get; a second register returns `false` and leaves the first data unchanged; ten parallel registers give exactly one `true`; children are returned only for their own mother; the raw stored row holds no plaintext name or email.
- [ ] Implement (atomic batch with `SubmitTransactionAsync`; a 409 becomes `false`), run the tests, commit: `Add encrypted parent storage`.

### Task 4: Register and Me endpoints

**Files:** create `Contracts/RegisterContracts.cs`, `Services/{RegistrationService}.cs`, `Controllers/V1/RegistrationController.cs`, test `Controllers/RegistrationTests.cs`. Remove the temporary test endpoint from Task 2 (repoint those tests at `GET /v1/me`).

- [ ] Failing tests:
  - Register with a valid body → 201, and `GET /v1/me` returns the same data with the name and email taken from the token.
  - Register twice → 409 `already-registered`.
  - `GET /v1/me` before register → 404 `not-registered`.
  - Consent `false` → 400. Zero children → 400. Seven children → 400. Class 0 and 11 → 400. A birth year out of range → 400. A language not on the list → 400. An overlong or control-character name → 400.
  - A second mother cannot see the first mother's data (`GET /v1/me` returns 404).
  - Storage failure (repository mocked to throw) → 503 with a fixed title and no exception text in the body.
  - The capturing logger never sees the uid, email or names during register.
- [ ] Implement, run all tests, commit: `Add register and me endpoints`.

### Task 5: GitHub Actions

**Files:** create `.github/workflows/api.yml`. Modify `Lantern.Api.csproj` (`ContainerRepository=lantern-api`, `ContainerFamily=noble-chiseled`).

- Triggers: pull requests and pushes to `main`, filtered to the API paths and the workflow.
- Job `build-test`: checkout, setup-dotnet from `global.json`, setup-node 22, `npm i -g azurite@3`, `dotnet build`, `dotnet test`.
- Job `image` (needs `build-test`): `dotnet publish -t:PublishContainer` to a tarball; smoke test (`docker load`, run in Production with dummy settings, `/health/live` returns 200 and `POST /v1/register` returns 401); Trivy on the tarball; upload the tarball for 7 days.
- Job `publish` (only on push to `main`, `packages: write`): log in to `ghcr.io` with `GITHUB_TOKEN`, publish `sha-<sha>` and `latest`.
- Actions pinned to SHAs:
```
actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1         # v7.0.1
actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68     # v6.0.0
actions/setup-node@820762786026740c76f36085b0efc47a31fe5020       # v7.0.0
actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a  # v7.0.1
docker/login-action@dbcb813823bdd20940b903addbd779551569679f      # v4.6.0
aquasecurity/trivy-action@ed142fd0673e97e23eac54620cfb913e5ce36c25 # v0.36.0
```
- [ ] Check locally: the container tarball builds without Docker (`-p:ContainerArchiveOutputPath`), the YAML parses, and `grep -E 'uses: [^@]+@v'` finds no unpinned action.
- [ ] Commit: `Add the API workflow: build, test, container image`.

### Task 6: Docs

**Files:** create `docs/runbooks/firebase-auth.md`. Modify `docs/ideation/decision-log.md` (append D17), `README.md` (Status).

- Runbook: Firebase project `lantern-ai-bg1709`; Google provider only; how to generate `Security:Key` (`openssl rand -base64 32`) and keep it in `dotnet user-secrets`; running locally with Azurite; getting a real ID token by hand (OAuth Playground, then `accounts:signInWithIdp`, with the web API key kept in an environment variable and restricted in the Google Cloud console); the Android app registration that issue #25 will need.
- D17: JwtBearer only; Google-only provider set in the console; revoked or disabled Firebase users stay valid until the ID token expires (at most 1 hour), accepted for v1; HMAC-keyed partition key; interim in-code encryption, with the Key Vault envelope scheme and account deletion (crypto-shredding) left to #23; registration collects consent (DPDP); GHCR images from `main` only.
- [ ] Commit: `Document Firebase setup and D17`.

## Verification

1. `dotnet build Lantern.slnx -c Release` gives 0 warnings.
2. `dotnet test --solution Lantern.slnx -c Release` is green, including the Azurite tests.
3. Manual: run the API against Azurite with a real ID token for `lantern-ai-bg1709`. `POST /v1/register` gives 201, a repeat gives 409, `GET /v1/me` returns the data, and a call with no token gives 401.
4. The container tarball builds. On a PR, `build-test` and `image` pass and `publish` is skipped.

## Out of scope

Add, edit and delete child after registration; account deletion; Key Vault envelope encryption (#23); Azure deployment (#19); the Flutter screens (#25).
