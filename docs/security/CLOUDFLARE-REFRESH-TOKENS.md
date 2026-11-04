# Cloudflare Refresh Token Storage — Security & Migration Plan

**State: DESIGN / PROTOTYPE ONLY. Not deployed, not wired into production authentication.** Current runtime still stores SHA-256 hashes in `LongevityDietDb.RefreshTokens` through EF Core. Never claim the Worker is the active token store without successful deployment + integration tests.

## Why D1 instead of KV

Token rotation must be single-use and atomic. Cloudflare Workers KV is eventually consistent; Cloudflare explicitly advises against relying on KV for atomic read-modify-write transactions (https://developers.cloudflare.com/kv/concepts/how-kv-works/). Cloudflare D1 batch statements execute in a SQL transaction and roll back if a statement fails (https://developers.cloudflare.com/d1/worker-api/d1-database/). Cloudflare Workers Secrets are appropriate for secret API credentials (https://developers.cloudflare.com/workers/configuration/secrets/).

## Confidentiality and trust boundaries

- Keep **raw refresh tokens only in HttpOnly, Secure, SameSite=Strict browser cookies** and transient ASP.NET Core memory during refresh. Never log them; never put them in localStorage or D1.
- Store only SHA-256 hashes and session state in Cloudflare D1. Restrict Cloudflare Worker access to a trusted backend, authenticated via strong server-to-server credentials (Cloudflare Access service tokens and TLS/mTLS where available); the prototype also checks `SERVICE_BEARER` as defense in depth.
- Configure Worker credentials through `wrangler secret put SERVICE_BEARER`; no credentials in `wrangler.toml`, Git, browser, CI logs or shared examples.
- The existing JWT signing key is a separate secret and remains server-side; do not move it into a D1 table.
- The public browser does not call the Cloudflare Worker, and cannot access the service secret.
- Strict same-origin routes and CSRF defenses for cookie-based state-changing endpoints. Use `Secure=true` in production behind HTTPS/proper forwarded headers.
- Use audit events without token hashes, medical information or secrets.

## Proposed data path

```text
Browser --HttpOnly cookie--> ASP.NET Core Identity API
  Identity API: SHA256(rawRefreshToken) in memory
  Identity API --authenticated HTTPS--> Cloudflare Worker
  Cloudflare Worker --D1 binding--> refresh_sessions (hash, user_id, expiry, revoked, lineage)
  Identity API <--metadata + atomic rotate status-- Worker
  Identity API --new HttpOnly cookie, signed access JWT--> Browser
```

For rotation: Identity validates SQL User active, Cloudflare Worker performs conditional insert(new hash) + revoke(old hash) in one D1 batch, then Identity issues a new session to browser. Replay must fail, including two concurrent refreshes. Network failures must fail closed instead of silently minting tokens or falling back to SQL against inconsistent state.

## Configuration / migration gates

1. Provision and authorize the Cloudflare account, D1 database, and private Worker route. Replace the placeholder D1 database ID in `cloudflare/refresh-sessions/wrangler.toml`; apply migration. Attach secret and appropriate Access policy.
2. An optional .NET `CloudflareRefreshTokenRepository` adapter now implements `IRefreshTokenRepository` and is selected by `CloudflareSessions:Enabled=true`. It uses authenticated HTTPS, hash-only payloads, a five-second timeout and atomic D1 rotation. Confirm per-environment secret injection, D1 deployment and account permissions before enabling it; SQL is still the default provider.
3. Add integration tests for register, login, logout/revoke, refresh rotation, expired/revoked token, token reuse/replay, two concurrent refreshes, Cloudflare failure, User disabled, and cookie behavior behind HTTPS.
4. Migrate existing sessions by either safely expiring legacy sessions and requiring a new login, or performing a carefully staged one-way hash import; **do not dual-write non-transactionally** or permanently accept refresh tokens from both stores.
5. Switch Identity auth behind a reviewed, explicit feature flag only after Cloudflare has passed E2E tests and recovery drills. Disable legacy refresh reads once cutover is complete.
6. Set monitoring/alerts on failed rotations, D1 errors and unauthorized requests; configure token retention, cleanup, secret rotation and D1 backups.
7. Rollback must not re-enable already revoked sessions. Prefer requiring reauthentication over violating revocation integrity.

## Working tree status

- Worker prototype: `cloudflare/refresh-sessions/src/index.mjs`
- Proposed schema: `cloudflare/refresh-sessions/migrations/0001_create_refresh_sessions.sql`
- Wrangler example: `cloudflare/refresh-sessions/wrangler.toml`
- **No Cloudflare account credentials were accessed, no Worker was deployed, and .NET has not been cut over.**

## Acceptance criteria for production use

- No raw refresh-token string in any data store or log.
- Atomic rotation tested at concurrency, including replay; exactly one winner.
- D1 data is not browser-accessible; Worker only accepts authenticated backend requests.
- All cookies secure on production HTTPS.
- No trust-boundary regression, and auth system continues to pass complete E2E suite.
