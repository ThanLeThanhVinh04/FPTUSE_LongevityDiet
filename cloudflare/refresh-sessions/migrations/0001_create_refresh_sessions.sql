-- Cloudflare D1 / SQLite. Proposed isolated session-hash store, NOT deployed.
CREATE TABLE IF NOT EXISTS refresh_sessions (
  id TEXT PRIMARY KEY NOT NULL,
  user_id TEXT NOT NULL,
  token_hash TEXT NOT NULL UNIQUE CHECK (length(token_hash) = 64),
  family_id TEXT NOT NULL,
  created_at INTEGER NOT NULL,
  expires_at INTEGER NOT NULL,
  revoked_at INTEGER,
  replaced_by_token_id TEXT,
  CHECK (expires_at > created_at)
);
CREATE INDEX IF NOT EXISTS idx_refresh_sessions_user_expiry
  ON refresh_sessions(user_id, expires_at);
CREATE INDEX IF NOT EXISTS idx_refresh_sessions_family
  ON refresh_sessions(family_id);
CREATE INDEX IF NOT EXISTS idx_refresh_sessions_expiry
  ON refresh_sessions(expires_at);
CREATE TABLE IF NOT EXISTS session_replay_audit (
  id TEXT PRIMARY KEY NOT NULL,
  token_id TEXT NOT NULL,
  detected_at INTEGER NOT NULL,
  action TEXT NOT NULL,
  correlation_id TEXT
);
CREATE INDEX IF NOT EXISTS idx_session_audit_token_time
  ON session_replay_audit(token_id, detected_at);
