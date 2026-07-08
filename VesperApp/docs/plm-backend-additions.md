# plm-cdk backend additions (future work)

These are the backend changes needed for the unified desktop suite and the Linux
base-station daemon to integrate cleanly. **None are implemented yet** — this is a
spec to drive a separate feature on the `plm-cdk` repo. They are small additions, not
blockers; the desktop/daemon stubs in VesperApp are written against the contract these
describe.

Reference paths in `plm-cdk` (as explored):
- API routes: `infra/lib/stacks/api-stack.ts`
- Auth middleware: `backend/src/middleware/auth.ts`
- Shared types: `packages/shared/src/types/index.ts`
- Org/entitlement handler: `backend/src/handlers/customers/index.ts`
- Closest client reference: `factory-sw/core/api_client.py`

---

## 1. Self-service entitlements endpoint

**Problem.** A customer cannot query their own org tier or entitlements today. Org
tier lives on `OrgItem.tier` (`free`|`paid`) and is only reachable via admin-only
routes. The desktop shell needs to gate modules on the signed-in account without admin
access.

**Add.** `GET /portal/me` (customer Cognito JWT), returning the caller's identity +
entitlements distilled from the JWT claims + the org record:

```json
{
  "userId": "…", "email": "…",
  "orgId": "acme", "orgName": "ACME Corp",
  "tier": "paid",
  "portalRole": "admin",
  "entitlements": ["cloud.sync", "data.upload", "gnss.postprocess"]
}
```

- Source tier/expiry from `OrgItem`; role from `custom:portalRole`.
- Introduce a per-org `entitlements: string[]` (store on `OrgItem.meta` or a dedicated
  attribute). Keys match `ASD.Contracts.Entitlements` (`cloud.sync`, `data.upload`,
  `gnss.postprocess`, `ble.remote_download`, `data.history_export`).
- Optionally also surface entitlements as a `custom:entitlements` Cognito claim so the
  client can gate without a round-trip.

Maps to C# `IPlmClient.SignInAsync` → `PlmSession.Account.Entitlements`.

---

## 2. Typed bulk data-ingest endpoint

**Problem.** Ingestion today is single-record `POST /telemetry` (Sigfox-shaped, API
key) or admin-only `POST /telemetry/manual`. A base-station daemon uploads batches of
downloaded wildlife data and needs a typed, scoped path. The presigned-doc path
(`/documents/upload-url` → S3 → `/documents/confirm`) works as a **stop-gap** but is
untyped for time-series.

**Add.** `POST /portal/ingest` (or an S3 ingest-bucket + EventBridge processor):
- Accepts a batch manifest + S3 object key(s) of uploaded captures.
- Scoped to the caller's org (JWT) or a base-station credential (see #3).
- Validates device ownership before associating records.

Until this exists, the daemon uses the presigned-S3 stop-gap; the C# contract already
models that as `IPlmClient.RequestUploadUrlAsync` / `ConfirmUploadAsync`.

---

## 3. Base-station credential type

**Problem.** A field daemon should authenticate as a machine with **ingest-only**,
individually revocable credentials — not a factory ops key (too broad) and not a user
login (no human).

**Add.** A 4th API-key class (alongside telemetry / read / ops) **or** per-station
Cognito machine credentials (client-credentials flow), scoped to ingest + read-own
routes only. Each deployed base station gets its own revocable credential.

Defined in `api-stack.ts` (key + usage plan) and enforced in `middleware/auth.ts`
via a new `requireApiKeyType(event, 'station')`.

---

## 4. Public config-discovery endpoint

**Problem.** Clients hardcode the API base URL + Cognito pool/client IDs per
environment (the `VITE_*` vars). New native clients (desktop, daemon) should not ship
stack outputs baked in.

**Add.** `GET /config` (public, unauthenticated) returning per-environment client
config:

```json
{
  "apiUrl": "https://…/v1",
  "cognitoUserPoolId": "us-east-1_…",
  "cognitoAppClientId": "…",
  "region": "us-east-1"
}
```

The desktop app fetches this once at first run (given only an environment name /
discovery host) and caches it. Keeps pool IDs out of the client binaries.

---

## Summary

| # | Endpoint / change          | Auth            | Consumer                  |
|---|----------------------------|-----------------|---------------------------|
| 1 | `GET /portal/me`           | customer JWT    | desktop entitlement gating|
| 2 | `POST /portal/ingest`      | JWT or station  | base-station daemon       |
| 3 | base-station credential    | new key class   | base-station daemon       |
| 4 | `GET /config`              | public          | desktop + daemon bootstrap|

None of these is required for the open-source local-only build to function; they light
up cloud sync, entitlement gating, and field-data upload when the proprietary plugins
are present.
