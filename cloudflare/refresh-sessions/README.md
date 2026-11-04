# Cloudflare D1 Refresh Session Store — PROPOSED, NOT DEPLOYED

This source is an optional security-oriented storage service. **A feature-flagged .NET adapter exists, but remains OFF and is not deployed.** The current default system still stores refresh token hashes in SQL Server. Do not advertise a Cloudflare migration as completed.

## Design and source
- `src/index.mjs`: private Worker endpoints for issue / lookup / atomic rotate / revoke. Uses SHA-256 token **hashes** and expiry metadata only.
- `migrations/0001_create_refresh_sessions.sql`: D1 schema.
- `wrangler.toml`: placeholder database_id, not a real Cloudflare account.
- `docs/security/CLOUDFLARE-REFRESH-TOKENS.md`: deployment and transition plan.

## Provisioning in your own Cloudflare account
1. Create a Cloudflare D1 database `longevity-refresh-session-store`.
2. Replace `database_id` in wrangler.toml with the actual D1 database UUID.
3. Apply the SQL migration to D1 after checking your environment.
4. Set `SERVICE_BEARER` with `wrangler secret put SERVICE_BEARER` (do not commit it).
5. Route the Worker behind access controls. `workers_dev=false` intentionally avoids automatic public workers.dev access. Prefer private/service-bound access or strict Cloudflare Access + mTLS.
6. Implement and test the .NET session-store adapter, cutover and safe rollback, then deploy.

No Worker secret, actual D1 ID, or credential is created by this repository. D1 transactions must be verified against concurrent rotate/replay tests before use. Do not use Workers KV as the authoritative rotation store because it is eventually consistent.
