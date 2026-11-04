// Cloudflare Worker: proposed private service for refresh-session hashes.
// Never send raw refresh tokens to this Worker or persist them in D1.
const noStore = { "Cache-Control": "no-store", "Content-Type": "application/json; charset=utf-8" };
const sha256Pattern = /^[0-9A-F]{64}$/;
const idPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
function reply(status, body) { return new Response(JSON.stringify(body), { status, headers: noStore }); }
function validHash(x) { return typeof x === "string" && sha256Pattern.test(x); }
function validId(x) { return typeof x === "string" && idPattern.test(x); }
function validUnix(x) { return Number.isSafeInteger(x) && x > 1600000000 && x < 5000000000; }
function bad() { return reply(400, { error: "invalid_request" }); }
async function jsonBody(request) {
  if (!(request.headers.get("content-type") || "").toLowerCase().includes("application/json")) return null;
  const length = Number(request.headers.get("content-length") || "0");
  if (length > 3072) return null;
  const text = await request.text();
  if (text.length > 3072) return null;
  try { const b = JSON.parse(text); return b && typeof b === "object" && !Array.isArray(b) ? b : null; } catch { return null; }
}
export default {
  async fetch(request, env) {
    if (request.method !== "POST") return reply(405, { error: "method_not_allowed" });
    // Shared secret is bound via 'wrangler secret put SERVICE_BEARER' (never vars or repo).
    if (!env.SERVICE_BEARER || request.headers.get("Authorization") !== "Bearer " + env.SERVICE_BEARER) {
      return reply(401, { error: "unauthorized" });
    }
    if (!env.DB) return reply(503, { error: "storage_not_configured" });
    const body = await jsonBody(request);
    if (!body) return bad();
    const now = Math.floor(Date.now() / 1000);
    const path = new URL(request.url).pathname;
    try {
      if (path === "/v1/sessions/issue") {
        if (!validId(body.id) || !validId(body.userId) || !validHash(body.tokenHash) ||
            !validId(body.familyId) || !validUnix(body.createdAt) || !validUnix(body.expiresAt) ||
            body.expiresAt <= now || body.expiresAt <= body.createdAt || body.expiresAt > now + 90*86400) return bad();
        const result = await env.DB.prepare(
          "INSERT INTO refresh_sessions (id,user_id,token_hash,family_id,created_at,expires_at) VALUES (?,?,?,?,?,?)"
        ).bind(body.id, body.userId, body.tokenHash, body.familyId, body.createdAt, body.expiresAt).run();
        return reply(result.success ? 201 : 503, { created: !!result.success });
      }
      if (path === "/v1/sessions/lookup") {
        if (!validHash(body.tokenHash)) return bad();
        const row = await env.DB.prepare(
          "SELECT id,user_id,family_id,created_at,expires_at,revoked_at FROM refresh_sessions WHERE token_hash = ?"
        ).bind(body.tokenHash).first();
        if (!row || row.revoked_at !== null || row.expires_at <= now) return reply(401, { error: "invalid_refresh_token" });
        return reply(200, { id:row.id, userId:row.user_id, familyId:row.family_id, expiresAt:row.expires_at });
      }
      if (path === "/v1/sessions/rotate") {
        if (!validHash(body.oldTokenHash) || !validHash(body.newTokenHash) || !validId(body.newId) ||
            !validUnix(body.expiresAt) || body.expiresAt <= now || body.expiresAt > now + 90*86400) return bad();
        // Cloudflare D1 batch is transactional. Insert only when the old token
        // is ACTIVE, then revoke old token inside the same transaction.
        // On replay, INSERT SELECT returns zero rows, then UPDATE changes zero.
        const results = await env.DB.batch([
          env.DB.prepare(
            "INSERT INTO refresh_sessions (id,user_id,token_hash,family_id,created_at,expires_at) SELECT ?,user_id,?,family_id,?,? FROM refresh_sessions WHERE token_hash=? AND revoked_at IS NULL AND expires_at>?"
          ).bind(body.newId,body.newTokenHash,now,body.expiresAt,body.oldTokenHash,now),
          env.DB.prepare(
            "UPDATE refresh_sessions SET revoked_at=?,replaced_by_token_id=? WHERE token_hash=? AND revoked_at IS NULL AND expires_at>?"
          ).bind(now,body.newId,body.oldTokenHash,now)
        ]);
        const insertedNew = results[0]?.meta?.changes === 1;
        const changedOld = results[1]?.meta?.changes === 1;
        return changedOld && insertedNew ? reply(200, { rotated:true }) : reply(401, { error:"invalid_refresh_token" });
      }
      if (path === "/v1/sessions/revoke") {
        if (!validHash(body.tokenHash)) return bad();
        await env.DB.prepare(
          "UPDATE refresh_sessions SET revoked_at=? WHERE token_hash=? AND revoked_at IS NULL"
        ).bind(now,body.tokenHash).run();
        return reply(200, { revoked:true });
      }
      return reply(404, { error:"not_found" });
    } catch (err) {
      // Deliberately never print payloads, token hashes or credentials in logs.
      return reply(503, { error:"session_store_unavailable" });
    }
  },
  async scheduled(_event,env,_ctx) {
    // Preserve expired metadata for up to 30 days for operational investigations.
    await env.DB.prepare("DELETE FROM refresh_sessions WHERE expires_at < ?")
      .bind(Math.floor(Date.now()/1000) - 30*86400).run();
  }
};
