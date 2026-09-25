# Lantern API: Firebase authentication (PR 1 of 2) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development or superpowers:executing-plans. Steps use `- [ ]`. On approval this plan is saved to `docs/superpowers/plans/2026-09-25-api-firebase-auth.md` and work starts on branch `feature/api-auth` cut from `origin/main`.

## Context

Lantern has only the NCERT build tool (Python). The runtime API (ASP.NET, .NET 10, D10) doesn't exist yet. The goal: a mother signs in with Google via Firebase in the app, and the API authenticates her and lets her build an account (one parent, many children).

After review this is split into **two PRs**:

- **PR 1 (this plan, full detail):** solution scaffold and conventions, Firebase JWT authentication, pseudonymous identity → `familyId` mapping, and the first controller, `SessionController`. Also the first GitHub Actions workflow: build, test, container image, and GHCR push from `main` only. This closes issue **#22**.
- **PR 2 (outlined at the end, gets its own detailed plan after PR 1 merges):** account profile (language, consent), children CRUD, per-family envelope encryption with a Key Vault master key, and the crypto-shredding delete path.

**Goal (PR 1):** A Firebase-authenticated API that maps a Google-signed-in parent to a stable, pseudonymous `familyId`, with CI that builds, tests and publishes a container image.

**Architecture:** One ASP.NET controller app, `apps/lantern-api/Lantern.Api`. It follows the layout of the sibling repo `../vantage-creative-approval/apps/approval-api`: `Controllers/V1`, `Contracts`, `Services`, `Repository`, `Auth`, `Configuration`, `Logging`, and exception mapping to ProblemDetails.

- JwtBearer validates Firebase ID tokens against Google's published keys.
- The uid is never stored. The `identities` table is keyed on `HMAC-SHA256(uid, secret)`, which maps to a random `familyId`.
- Tests exercise the **real** JwtBearer pipeline with a test RSA signing key, injected only by the test project's `WebApplicationFactory`. There is no auth bypass anywhere in the app.

**Tech stack:** everything below was checked against the NuGet index on 2026-09-25.

- .NET SDK 10.0.112 (installed), ASP.NET Core controllers.
- `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.12, `Azure.Data.Tables` 12.13.0, `Azure.Identity` 1.21.0.
- Tests: xunit.v3 4.0.1 on Microsoft.Testing.Platform, `Microsoft.AspNetCore.Mvc.Testing` 10.0.12, Moq 4.20.72, coverlet.collector 10.0.1.
- Azurite, installed via npm both locally and in CI.
- SDK container publish (no Dockerfile).

**Spec:** issue #22; `docs/ideation/decision-log.md` D3/D9/D10; this session's review. The decisions made here are recorded as **D17** (Task 6).

## Global Constraints

- Conventions come from `../vantage-freight-hub` / `../vantage-creative-approval`:
  - `Directory.Build.props` sends output to `dist/`, with net10.0, Nullable, `TreatWarningsAsErrors`, `AnalysisMode=Recommended`, and `EnforceCodeStyleInBuild`.
  - Central package management goes in `Directory.Packages.props`, and `NuGet.config` clears feeds down to nuget.org.
  - `global.json` pins `10.0.100` with `rollForward: latestFeature` and `test.runner: Microsoft.Testing.Platform`.
- **`ConfigureAwait(false)` is not used.** ASP.NET Core has no SynchronizationContext, so it only adds noise. If CA2007 fires, turn it off in the root `.editorconfig` with that reason as the comment.
- Every log line goes through the source-generated `Logging/Log.cs`, and there are no interpolated log strings.
- Options are validated on start: `.ValidateDataAnnotations().ValidateOnStart()`. A missing setting stops the app from starting.
- **No Google PII anywhere:**
  - Never persist or log the email, name, photo, raw token, or raw uid.
  - The uid exists only in memory during a request. At rest it is `HMAC-SHA256(uid, Identity:UidHashKey)` as lowercase hex.
  - Logs carry `familyId` and short reason codes only.
- **Leak paths closed:**
  - `IdentityModelEventSource.ShowPII` is left `false`, and a test asserts it.
  - `JwtBearerOptions.IncludeErrorDetails = false`.
  - The ProblemDetails mapper uses fixed titles and codes and never echoes `exception.Message`.
  - No `UseHttpLogging` or header logging is registered. If it is ever added, `Authorization` is excluded; this is noted in the API README.
- **Firebase token rules:**
  - The issuer is pinned to `https://securetoken.google.com/{projectId}`, and the audience to `{projectId}`.
  - Lifetime is validated (clock skew 2 min) and the signing key is validated.
  - `sub` must be present and match `^[A-Za-z0-9_-]{1,128}$`.
  - `firebase.sign_in_provider` must be `google.com`.
- No runtime test-token or dev-auth mode exists in the app. For local manual testing, use a real Firebase ID token (see the runbook).
- CI: third-party actions are **pinned to commit SHAs** (below). Images are pushed **only on `push` to `main`**, to GHCR with `GITHUB_TOKEN`. Azurite is installed in the workflow with `npm i -g azurite@3`.
- Test names use `Method_Scenario_Expected`. The tests' `.editorconfig` switches off CA1707, CA1711 and xUnit1051.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

Pinned actions:
```
actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1         # v7.0.1
actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68     # v6.0.0
actions/setup-node@820762786026740c76f36085b0efc47a31fe5020       # v7.0.0
actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a  # v7.0.1
docker/login-action@dbcb813823bdd20940b903addbd779551569679f      # v4.6.0
aquasecurity/trivy-action@ed142fd0673e97e23eac54620cfb913e5ce36c25 # v0.36.0
```

## Review Focus

1. **Two first sign-ins at once for the same uid** (app retry, double tap) → one `familyId`. This relies on a conditional insert (`AddEntityAsync`, 409 → re-read). (Task 3 concurrent test, and a Task 4 end-to-end test with parallel requests.)
2. **Token for another Firebase project, a wrong issuer, an expired token, an untrusted key, or a non-Google provider** (`anonymous`, `password`) → 401 with no `identities` row written. (Task 4 theory.)
3. **401 responses leak nothing**: no `error_description` in `WWW-Authenticate`, and no exception text in the body. (Task 4.)
4. **Storage outage** → 503 ProblemDetails with a fixed title and no stack trace or exception message. (Task 4.)
5. **uid, email or name never show up in logs or in the table.** A capturing `ILoggerProvider` scans every message. A raw table read asserts that the PartitionKey is not the uid. (Task 3 and Task 4.)

Known and accepted for v1 (goes in D17): JwtBearer can't see Firebase **revoked or disabled** users until their ID token expires (at most 1 hour). A revocation check with the Admin SDK comes later, if it's ever needed.

## Storage layout (PR 1)

| Table | PartitionKey | RowKey | Columns |
|---|---|---|---|
| `identities` | `HMAC-SHA256(uid, UidHashKey)` hex | `family` | `FamilyId` (Guid "D"), `LinkedAt` (UTC) |

The table is created lazily, once per process (`CreateIfNotExistsAsync`). `UidHashKey` must never be rotated without a migration, because rotating it orphans every link (noted in D17). Production supplies it as a Container Apps secret reference to Key Vault (#19). Development and tests use throwaway values.

## API surface (PR 1)

| Method | Route | Auth | Result |
|---|---|---|---|
| POST | `/v1/session` | Firebase bearer | Get or create the caller's family. **201** `{familyId, isNew:true}` on the first call, **200** `{familyId, isNew:false}` after that |
| GET | `/health/live` | anonymous | 200 |

The fallback authorization policy is `RequireAuthenticatedUser`, so every endpoint is closed unless it says `[AllowAnonymous]`.

---

### Task 1: Solution scaffold, health endpoint, test harness

**Files (create):**
- Root: `Lantern.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `NuGet.config`, `.editorconfig`
- App: `apps/lantern-api/Lantern.Api/{Lantern.Api.csproj, Program.cs, appsettings.json, appsettings.Development.json, Properties/launchSettings.json}`
- Tests: `apps/lantern-api/Lantern.Api.Tests/{Lantern.Api.Tests.csproj, .editorconfig, Infrastructure/AzuriteFixture.cs, Infrastructure/LanternApiFactory.cs, Infrastructure/ApiCollection.cs, HealthTests.cs}`

**Modify:** `.gitignore` (add `dist/`, `bin/`, `obj/`, `TestResults/`, `*.trx`, `__azurite*`, `.env`, `*.user`)

**Produces:**
- `AzuriteFixture.ConnectionString`.
- `LanternApiFactory : WebApplicationFactory<Program>`, environment `Testing`, built with a connection string. It exposes `CreateClient()` and `Services`, and is shared through `[Collection(ApiCollection.Name)]`.

- [ ] Copy the props, packages, global.json and NuGet.config shapes from `../vantage-freight-hub/`, using the versions in the Tech stack above.
- [ ] Set up the test project the way `../vantage-freight-hub/apps/freight-hub-tests/Vantage.Freight.Hub.Tests.csproj` does: `OutputType Exe`, `TestingPlatformDotnetTestSupport`, `<Using Include="Xunit"/>`. Copy its `.editorconfig`.
- [ ] Port `../vantage-creative-approval/apps/approval-api/Vantage.Approvals.Api.Tests/TableStorage/AzuriteTableFixture.cs` to xunit.v3 (`IAsyncLifetime` returns `ValueTask`). It starts `azurite-table` on a free port.
- [ ] Failing test `HealthTests.Live_Anonymous_Returns200`. Run `dotnet test --solution Lantern.slnx`: it fails.
- [ ] Minimal `Program.cs`: `AddControllers`, `AddProblemDetails`, `AddHealthChecks`, `MapHealthChecks("/health/live", new() { Predicate = _ => false }).AllowAnonymous()`. .NET 10 generates `public partial class Program`; add it by hand only if the factory can't see it.
- [ ] The test passes. Commit: `Scaffold the Lantern API solution and test harness`.

### Task 2: Firebase token rules (pure)

**Files:** Create `Lantern.Api/Auth/FirebaseTokenRules.cs` and `Lantern.Api.Tests/Auth/FirebaseTokenRulesTests.cs`

**Produces:**
```csharp
public static partial class FirebaseTokenRules
{
    public const string SubjectClaim = "sub";
    public const string FirebaseClaim = "firebase";
    public const string GoogleProvider = "google.com";

    /// <summary>Null when the principal is acceptable; otherwise a short reason code that is safe to log.</summary>
    public static string? FindViolation(ClaimsPrincipal principal);

    /// <summary>The Firebase uid. Throws InvalidOperationException when absent (only after FindViolation passed).</summary>
    public static string GetUid(ClaimsPrincipal principal);

    [GeneratedRegex("^[A-Za-z0-9_-]{1,128}$")]
    private static partial Regex UidPattern();
}
```
Reason codes: `missing-subject`, `malformed-subject`, `missing-firebase-claim`, `malformed-firebase-claim` (not JSON, or no `sign_in_provider` string), and `sign-in-provider-not-allowed`. The `firebase` claim arrives as a JSON string (`JsonClaimValueTypes.Json`) and is parsed with `JsonDocument`.

- [ ] Failing tests:
  - A theory covering each reason code.
  - `FindViolation_GoogleProvider_ReturnsNull`.
  - A theory: `anonymous`, `password` and `phone` → `sign-in-provider-not-allowed`.
  - Build principals with `new Claim("firebase", "{\"sign_in_provider\":\"google.com\"}", JsonClaimValueTypes.Json)`.
- [ ] Implement, get the tests to pass, and commit: `Add Firebase token rules`.

### Task 3: Pseudonymous identity links (Azurite-backed)

**Files (create):** `Configuration/StorageOptions.cs`, `Configuration/IdentityOptions.cs`, `Services/{IUidHasher.cs, HmacUidHasher.cs}`, `Repository/{IIdentityLinks.cs, TableIdentityLinks.cs}`, and tests `Services/HmacUidHasherTests.cs`, `Repository/IdentityLinksTests.cs`

**Produces:**
```csharp
public interface IUidHasher { string Hash(string uid); } // lowercase hex HMAC-SHA256
public interface IIdentityLinks
{
    Task<Guid?> FindFamilyAsync(string uidHash, CancellationToken cancellationToken);
    /// <summary>Conditional insert. Returns the stored familyId and whether this call created it.</summary>
    Task<(Guid FamilyId, bool Created)> LinkAsync(string uidHash, Guid candidateFamilyId, CancellationToken cancellationToken);
}
```
- `StorageOptions` (`IValidatableObject`, exactly one of the two): `ConnectionString` (Azurite/dev) or `TableEndpoint` (production, `new TableServiceClient(new Uri(endpoint), new DefaultAzureCredential())`, managed identity). Also `IdentitiesTable = "identities"`.
- `IdentityOptions`: `UidHashKey`, base64, and at least 32 bytes once decoded (custom validation).
- `LinkAsync`: `AddEntityAsync`; on `RequestFailedException { Status: 409 }`, `GetEntityAsync` and return `(existing, false)`. The table is ensured once through a `bool` flag plus `CreateIfNotExistsAsync`, which is idempotent, so a race there does no harm.

- [ ] Failing tests:
  - `Hash_SameUid_IsStable`.
  - `Hash_DifferentKeys_Differ`.
  - `Hash_UidsDifferingOnlyByCase_Differ`.
  - `Hash_Output_IsNotTheUid`.
  - Azurite: `LinkAsync_TwoConcurrentLinks_BothReturnTheFirstFamily` (uses `Task.WhenAll`; exactly one result has `Created=true`).
  - Azurite: `FindFamilyAsync_Unknown_ReturnsNull`.
  - Azurite: `LinkAsync_StoresUtcTime`.
- [ ] Implement, get the tests to pass, and commit: `Add pseudonymous identity links`.

### Task 4: Firebase JwtBearer and SessionController

**Files (create):**
- App: `Configuration/FirebaseOptions.cs`, `Auth/FirebaseAuthentication.cs`, `Services/{ISessionService.cs, SessionService.cs}`, `Contracts/SessionContracts.cs`, `Controllers/V1/SessionController.cs`, `Middleware/ProblemExceptionHandler.cs` (`IExceptionHandler`), `Logging/Log.cs`
- Tests: `Infrastructure/TestTokens.cs`, `Infrastructure/CapturingLoggerProvider.cs`, `Controllers/SessionControllerTests.cs`

**Modify:** `Program.cs`, `appsettings.json` (empty required keys), `appsettings.Development.json` (Azurite connection string, throwaway `UidHashKey`, `Firebase:ProjectId` from the runbook)

**Consumes:** `FirebaseTokenRules`, `IUidHasher`, `IIdentityLinks`
**Produces:** `ISessionService.StartAsync(string uid, CancellationToken) → SessionResult(Guid FamilyId, bool IsNew)`, which PR 2 reuses to resolve `familyId`. Also `TestTokens.Create(string uid, string projectId = "lantern-test", string provider = "google.com", DateTimeOffset? expires = null, SecurityKey? key = null, IDictionary<string, object>? extraClaims = null)` and `TestTokens.Key` (RSA-2048).

`FirebaseAuthentication.AddFirebaseAuthentication(this IServiceCollection services, IConfiguration configuration)`:
```csharp
options.Authority = $"https://securetoken.google.com/{projectId}";   // OIDC discovery → Google's JWKS
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
    OnTokenValidated = context =>
    {
        var violation = FirebaseTokenRules.FindViolation(context.Principal!);
        if (violation is not null) { Log.TokenRejected(logger, violation); context.Fail(violation); }
        return Task.CompletedTask;
    },
    OnAuthenticationFailed = context =>
    {
        Log.TokenInvalid(logger, context.Exception.GetType().Name);   // type name only, never the message
        return Task.CompletedTask;
    },
};
```
- The `ProblemExceptionHandler` covers only `RequestFailedException` → 503 `storage-unavailable`, and everything else → 500 `unexpected-error`. The titles are fixed strings and `detail` is never set from an exception.
- `SessionService.StartAsync`: `hash = hasher.Hash(uid)`, then `FindFamilyAsync`. If there is a link, return `(id, false)`. Otherwise `LinkAsync(hash, Guid.NewGuid())`. When `Created` is true, log `Log.FamilyCreated(familyId)`.
- Test harness: `LanternApiFactory` → `ConfigureTestServices(s => s.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o => o.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(new() { Issuer = "https://securetoken.google.com/lantern-test", SigningKeys = { TestTokens.Key } })))`. This replaces only the key source and keeps every validation rule from production. It also sets `Firebase:ProjectId=lantern-test`, the Azurite connection string, a fixed test `UidHashKey`, and adds `CapturingLoggerProvider`.

- [ ] Failing tests:
  - `Post_NoToken_Returns401`.
  - A theory of rejected tokens → 401 each: wrong audience, wrong issuer, expired, signed by a different RSA key, provider `anonymous`, provider `password`, sub `a/b`, no `firebase` claim. Each case asserts the `identities` table has no rows.
  - `Post_Rejected_WwwAuthenticateHasNoErrorDescription`.
  - `Post_FirstCall_Returns201IsNew`.
  - `Post_SecondCall_Returns200SameFamily`.
  - `Post_TenParallelFirstCalls_OneFamilyExactlyOne201`.
  - `Post_StoredPartitionKey_IsNotTheUid` (a raw `TableClient` read).
  - `Post_StorageThrows_Returns503ProblemWithFixedTitle`: swap `IIdentityLinks` for a Moq that throws `RequestFailedException(500, "secret-detail")`, and assert the body lacks `secret-detail`.
  - `AnyCall_NeverLogsUidEmailOrName`: the token carries `email` and `name` claims; scan every captured message, including the rejected-token paths.
  - `IdentityModel_ShowPII_IsFalse`.
- [ ] Implement, get the tests to pass, then run the full suite and commit: `Authenticate with Firebase and add the session controller`.

### Task 5: GitHub Actions — build, test, container artifact

**Files (create):** `.github/workflows/api.yml`, `.github/pull_request_template.md` (adapted from `../vantage-creative-approval/.github/pull_request_template.md`: area, knowledge update, validation)
**Modify:** `Lantern.Api.csproj`: `ContainerRepository=lantern-api`, `ContainerFamily=noble-chiseled` (non-root, port 8080)

`api.yml`:
- It runs on `pull_request` and on `push` to `main`, with a path filter on `apps/lantern-api/**`, `Lantern.slnx`, `Directory.*.props`, `global.json`, `NuGet.config`, `.editorconfig`, and the workflow itself. It sets `permissions: contents: read` at the top level.
- **build-test**:
  1. checkout, then setup-dotnet (`global-json-file: global.json`), then setup-node (`node-version: 22`), then `npm i -g azurite@3`.
  2. `dotnet build Lantern.slnx -c Release`.
  3. `dotnet test --solution Lantern.slnx -c Release --no-build --report-trx`.
  4. upload-artifact for the `.trx` files, `if: always()`.
- **image** (needs build-test; runs on PRs and main):
  1. `dotnet publish apps/lantern-api/Lantern.Api -c Release -t:PublishContainer -p:ContainerArchiveOutputPath=dist/image/lantern-api.tar.gz -p:ContainerImageTag=sha-${{ github.sha }}`.
  2. Smoke test:
     - `docker load -i dist/image/lantern-api.tar.gz`.
     - `docker run -d -p 8080:8080` with `ASPNETCORE_ENVIRONMENT=Production`, `Firebase__ProjectId=lantern-ci`, `Storage__TableEndpoint=https://ci.table.core.windows.net`, and `Identity__UidHashKey=$(openssl rand -base64 32)`.
     - Poll `curl -fsS localhost:8080/health/live` → 200, then `curl -o /dev/null -w '%{http_code}' -X POST localhost:8080/v1/session` → `401`.
  3. Trivy on the tarball (`input:`), severity CRITICAL/HIGH, `ignore-unfixed: true`, `exit-code: 1`.
  4. upload-artifact for the tarball with `retention-days: 7`.
- **publish**:
  - Runs `if: github.event_name == 'push' && github.ref == 'refs/heads/main'`, needs `image`, with `permissions: contents: read, packages: write`.
  - Steps: docker/login-action to `ghcr.io` with `${{ github.actor }}` / `${{ secrets.GITHUB_TOKEN }}`, then `dotnet publish ... -t:PublishContainer -p:ContainerRegistry=ghcr.io -p:ContainerRepository=bgandhi1709/lantern-api -p:ContainerImageTags='"sha-${{ github.sha }};latest"'`.

- [ ] Local checks (there's no Docker here):
  - `dotnet publish apps/lantern-api/Lantern.Api -c Release -t:PublishContainer -p:ContainerArchiveOutputPath=dist/image/lantern-api.tar.gz` → the tarball exists.
  - `actionlint` if available; otherwise `python3 -c "import yaml,sys; yaml.safe_load(open(sys.argv[1]))" .github/workflows/api.yml`.
  - `grep -E 'uses: [^@]+@v' .github/workflows/api.yml` finds nothing (every action is pinned to a SHA).
- [ ] Commit: `Add the API workflow: build, test, container image`.

### Task 6: Docs — Firebase runbook, D17, READMEs

**Files (create):** `docs/runbooks/firebase-auth.md`, `apps/lantern-api/README.md`
**Modify:** `docs/ideation/decision-log.md` (append **D17**, 2026-09-25), `README.md` (Status, and `apps/lantern-api` under "How it is built")

- Runbook:
  1. Create the Firebase project on the no-cost Spark plan. Enable the **Google** provider only.
  2. Add the Android app (package name, and the SHA-1 from `keytool -list -v -keystore ~/.android/debug.keystore`), which #25 needs.
  3. Copy the project ID into `Firebase:ProjectId` (it isn't secret).
  4. Generate `UidHashKey` with `openssl rand -base64 32` and keep it in `dotnet user-secrets` locally and in Key Vault in production. **Never rotate it without a migration.**
  5. Local run: `azurite --location /tmp/azurite-lantern &`, then `dotnet run --project apps/lantern-api/Lantern.Api`.
  6. Getting a **real** ID token for manual calls:
     - Get a Google access token (scopes `openid email`) from the OAuth 2.0 Playground.
     - `POST https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key=<web API key>` with `{"postBody":"access_token=<token>&providerId=google.com","requestUri":"http://localhost","returnSecureToken":true}`.
     - Use the returned `idToken` as the bearer for `POST /v1/session`.
- API README:
  - Commands.
  - Layering.
  - The PII rules and leak-path list, including "never add header logging without excluding `Authorization`".
  - Why tests inject a signing key instead of a fake auth handler: the fake would skip the issuer, audience, lifetime and provider checks that are the thing under test.
- D17 records:
  - Google-only provider.
  - HMAC-keyed identity links and the no-rotation rule.
  - Random `familyId`.
  - No runtime test tokens.
  - Revoked or disabled Firebase users stay valid until the token expires (at most 1 hour), accepted for v1.
  - GHCR images published from `main` only.
  - `ConfigureAwait(false)` dropped: it does nothing in ASP.NET Core. This is a noise decision, not a correctness rule.
  - The PR 2 scope below.
- [ ] Commit: `Document Firebase setup, D17 and the API`.

## Verification (PR 1, end to end)

1. `dotnet build Lantern.slnx -c Release` → 0 warnings (warnings are errors).
2. `dotnet test --solution Lantern.slnx -c Release` → all green, including the Azurite, auth-rejection, race and log-scan tests.
3. Manual run: `dotnet run` with a real Firebase project and a token from the runbook. `POST /v1/session` → 201, then 200 with the same `familyId`. No token → 401 with no error details. Check that the console logs show no uid or email.
4. The container publish tarball builds locally.
5. Push `feature/api-auth` and open a PR. `build-test` and `image` pass (the smoke test gets 200 and 401), and `publish` is skipped. After the merge, `publish` pushes `ghcr.io/bgandhi1709/lantern-api:sha-…` and `:latest`.

---

## PR 2 outline (gets its own detailed plan after PR 1 merges)

**Scope:** account profile, one parent → many children, envelope encryption, and crypto-shredding. Everything reuses `ISessionService.StartAsync` to resolve `familyId`.

- **IDOR rule:** every route takes `familyId` **only** from token → uid → HMAC → `identities`, never from the route, query or body. Routes are `/v1/account...` and `/v1/children/{childId}`, and none carries a familyId. Tests: parent A calls GET, PUT and DELETE on parent B's `childId` → **404**, with a body identical to a child that doesn't exist.
- **Account:**
  - `GET /v1/account`.
  - `PUT /v1/account/language` (`en`/`gu`/`hi`).
  - `POST /v1/account/consent {noticeVersion}` (records the UTC time; D3 order: language → consent → children).
  - Children can't be created before consent → 409 `consent-required`.
- **Children:**
  - `GET/POST /v1/children`, `GET/PUT/DELETE /v1/children/{childId}`.
  - Fields: nickname, school name (both encrypted); class 1–10 and birth year (plain).
  - A cap of 6 per family → 409 `child-limit-reached`.
  - **ETag optimistic concurrency:** responses carry `ETag`. `PUT` and `DELETE` require `If-Match` (missing → 428, stale → 412), which maps to the Table entity ETag.
- **Envelope encryption:**
  - Each family has a random 256-bit DEK.
  - The master key is a **Key Vault key** (RSA-OAEP-256 wrap/unwrap through `CryptographyClient`, `Azure.Security.KeyVault.Keys` 4.10.1) reached with **managed identity**. It is never an app setting.
  - The wrapped DEK is stored with the **Key Vault key version** that wrapped it, so the master key can be rotated.
  - Unwrapped DEKs are cached in memory for a short TTL.
  - Fields use AES-GCM with a **fresh random 96-bit nonce per encryption**, stored as `v1.base64(nonce|tag|ciphertext)`. The associated data is `familyId|childId|field`, so a ciphertext can't be swapped between records or fields; tests prove this.
  - Local development and tests use an `IKeyWrapper` test double that only the test project, or a Development-only flag that fails start-up elsewhere, can register. A start-up test asserts this.
  - Key Vault itself arrives with #19, or is created by hand from the runbook.
- **Crypto-shredding / DPDP:** `DELETE /v1/account` does three things in order:
  1. Delete the wrapped DEK, so every encrypted field is unreadable from that moment.
  2. Delete the children and account rows.
  3. Delete the identity link.

  Each step is idempotent, so a partial failure can be retried. Record it in D18. The DPDP Act 2023 specifics (verifiable parental consent, erasure timelines) are **flagged for legal confirmation** before the pilot.
- Out of scope for both PRs: raw append-only onboarding events (#20/#24), Azure deployment (#19), and the Flutter screens (#25).

## Execution

Recommended: **Native** execution. The six tasks form a straight chain of interfaces (rules → links → auth + session → CI → docs). The security-critical behaviour is pinned by explicit tests, and one fresh reviewer on the whole auth path at the end is where an independent look pays off most.
