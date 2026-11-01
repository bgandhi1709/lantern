# Delete a Child: end-to-end flow

The flow for #187 under ADR-0019, D125 and D126. The One-time code part is generic: the phone names the operation (`delete-child`) and its target, and `SecurityController` knows neither. Steps marked **new** are built in #187; the rest exists today (ADR-0003, ADR-0018).

## 1. Sequence

```mermaid
sequenceDiagram
    autonumber
    actor P as Parent
    participant A as App (Expo, RN)
    participant API as Lantern.Api (ASP.NET, Container App)
    participant ACS as Azure Communication Services Email
    participant T as Table storage
    participant MQ as CloudAMQP (exchange lantern.actions)
    participant F as Lantern.Functions (RabbitMQTrigger)
    participant B as Blob storage

    rect rgb(235, 245, 255)
    Note over P,A: Confirm step (all on the phone, no network)
    P->>A: Settings > Delete a child
    A->>A: useDeletableFamily lists Children, none picked<br/>one Child only: screen points to Delete Family
    P->>A: pick Child, type the name
    A->>A: Email me a code enabled only when typedName === child.name (case sensitive)
    end

    rect rgb(240, 255, 240)
    Note over A,ACS: Request a code (new)
    A->>API: POST /v1/security/code<br/>Bearer Firebase ID token<br/>{operation delete-child, target childId}
    API->>API: JWT validated, CallerIdentity(sub, email, email_verified)<br/>rate limit on POST /v1/security/code: 1 per min, 5 per hour per Parent (hash of uid)
    API->>API: email_verified false: 403 email-not-verified
    Note over API: no check of the target here (D126): the endpoint that acts on the proof does that
    API->>API: IOneTimeCodeGenerator: code = RandomNumberGenerator.GetInt32(0, 1,000,000) padded to 6<br/>nonce = 16 random bytes
    API->>API: payload = {operation delete-child, target childId, exp now+5min, nonce, sub uidHash}<br/>challenge = b64url(payload) . b64url(HMAC-SHA256(Security:Key, payload + code))
    API->>ACS: IEmailService.SendAsync(to token email, code)<br/>Azure.Communication.Email EmailClient, sender from Email:Sender
    alt send fails
        ACS--xAPI: error
        API-->>A: 502 email-not-sent, no challenge, nothing stored
    else accepted
        ACS-->>API: accepted
        API-->>A: 200 {challenge}
    end
    ACS-->>P: email with the 6-digit code (managed domain, may land in spam)
    A->>A: challenge kept in memory only (not on disk)
    end

    rect rgb(255, 250, 235)
    Note over P,API: Verify the code (new)
    P->>A: type the 6 digits
    A->>API: POST /v1/security/code/verify {challenge, code}
    API->>API: rate limit on verify: 5 per min per Parent, over: 429 code-attempts-used
    API->>API: split challenge, constant-time compare HMAC(payload + code)<br/>no match: 400 code-wrong<br/>exp passed: 410 code-expired<br/>sub not the caller: treated as wrong
    API->>API: proof = b64url(payload2) . b64url(HMAC(Security:Key, "proof" + payload2))<br/>payload2 = {operation, target, exp now+5min, sub uidHash}
    API-->>A: 200 {proof}
    A->>A: Delete child button turns on
    end

    rect rgb(255, 240, 240)
    Note over A,B: Delete (request is the commit point)
    P->>A: tap Delete child
    A->>API: DELETE /v1/family/children/{childId}<br/>header Lantern-Code-Proof: proof
    API->>API: verify proof signature, exp, sub == caller, operation == delete-child and target == childId<br/>fail: 403 proof-required
    API->>T: ChildService.RemoveAsync: single read in Family partition (404 child-not-found if another Family's, or unknown)
    API->>T: IActionLedger.RecordAsync(RemoveWorkspace, id familyId_childId)<br/>commit point, a repeat is the same row
    API->>T: ChildRepository.MarkDeletingAsync: Status = Deleting (merge, ETag.All)
    API->>MQ: IActionPublisher.SendAsync, key remove-workspace<br/>failure only logged, PendingActionResender retries
    API-->>A: 202 Accepted
    A->>A: write activeChildId = next Child, Home shows the other Child
    end

    rect rgb(245, 245, 245)
    Note over MQ,B: Erase (async, safe to repeat)
    MQ->>F: queue Actions:RemoveWorkspaceQueue (KEDA rabbitmq rule scales from zero)
    F->>T: ActionFunction.ExecuteAsync: ledger IsPending? no: skip
    F->>B: IWorkspaceStore.RemoveAsync(familyId, childId)
    F->>T: IProgressRepository.RemoveChildAsync (chapter, attempt, check rows)
    F->>T: IChildRepository.RemoveAsync (the Child row)
    F->>T: ledger CompleteAsync
    end

    Note over A,API: GET /v1/me never lists a Deleting Child, so the phone sees it gone as soon as the 202 returns
```

## 2. What each failure does on the screen

```mermaid
flowchart TD
    start(["Delete a child screen"]) --> pick["Pick Child, none preselected"]
    pick --> one{"Family has one Child?"}
    one -- yes --> pointer["Say it cannot be deleted, point to Delete Family"]
    one -- no --> name["Type the name exactly"]
    name --> match{"typed name equals Child name?"}
    match -- no --> off1["Email me a code stays off"]
    match -- yes --> req["POST security/code"]

    req --> r1{"Response"}
    r1 -- "429 code-too-soon" --> tooSoon["Say wait before asking again"]
    r1 -- "403 email-not-verified" --> notVerified["Say the Google email is not verified"]
    r1 -- "502 email-not-sent" --> notSent["Say the email could not be sent, nothing was created"]
    r1 -- "network or timeout" --> unreachable["Say the API is unreachable, keep the form"]
    r1 -- "200 challenge" --> code["Code step, challenge held in memory"]

    code --> typed["Type 6 digits"]
    typed --> ver["POST security/code/verify"]
    ver --> r2{"Response"}
    r2 -- "400 code-wrong" --> wrong["Say the code is wrong, stay on code step"]
    r2 -- "410 code-expired" --> expired["Say it expired, offer a new code"]
    r2 -- "429 code-attempts-used" --> tries["Say too many tries, wait a minute"]
    r2 -- "200 proof" --> armed["Delete child turns on"]

    armed --> del["DELETE child with proof header"]
    del --> r3{"Response"}
    r3 -- "403 proof-required" --> expired
    r3 -- "network or timeout" --> keep["Keep the proof, allow retry"]
    r3 -- "202" --> done["Next Child becomes active, Home"]

    cancel(["Cancel at any step"]) --> nocall["No delete call is made"]
```

## 3. Trust and checks, by hop

| Hop | What is checked | Where | Failure |
|---|---|---|---|
| App to API | Firebase ID token signature, issuer, audience | ASP.NET JWT bearer, `[Authorize]` | 401 |
| Request a code | `email_verified`, operation is a kebab name, rate limit. The target is not checked | `OneTimeCodeService`, rate limiter | 403, 400, 429 |
| The challenge | HMAC keyed by `Security:Key` over payload and code. The code is not in it, and the HMAC cannot be tested offline | `ICryptoService.Sign` | none, it is opaque |
| Verify | HMAC match (constant time), expiry, uid hash equals the caller's, 5 a minute | `OneTimeCodeService`, rate limiter | 400, 410, 429 |
| The proof | Separate HMAC key so a challenge is never a proof; operation, target and Parent bound; 5 minutes | `OneTimeCodeService` | 403 |
| Delete | Proof of `delete-child` for this Child and this Parent; the Child belongs to the Family | `ChildService.RemoveAsync` | 403, 404 |
| Erase | The ledger row is still pending; every step repeats safely | `ActionFunction`, `RemoveWorkspaceFunction` | the broker redelivers |

## 4. Stored where

| Thing | Stored | Lifetime |
|---|---|---|
| One-time code | Only in the email | Until the challenge expires (5 minutes) |
| Challenge | Phone memory | The screen's lifetime |
| Proof | Phone memory | 5 minutes, replayable for the same Child (accepted, deleting twice is safe) |
| Ledger row | Table storage `actions` | Until the Function completes it |
| Child row | Table storage `families`, Status `Deleting` then removed | Until the Function removes it |
| Workspace blobs, progress rows | Blob and Table storage | Until the Function removes them |
