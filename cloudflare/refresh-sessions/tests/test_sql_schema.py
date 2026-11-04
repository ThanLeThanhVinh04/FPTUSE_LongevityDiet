"""Cloudflare D1 SQLite schema check (local SQLite, not remote D1)."""
import sqlite3
import unittest
from pathlib import Path

MIGRATION = Path(__file__).resolve().parents[1] / "migrations" / "0001_create_refresh_sessions.sql"
NOW = 1_784_000_000
OLD_ID = "123e4567-e89b-42d3-a456-426614174001"
NEW_ID = "123e4567-e89b-42d3-a456-426614174002"
USER_ID = "123e4567-e89b-42d3-a456-426614174000"
FAMILY_ID = "123e4567-e89b-42d3-a456-426614174004"
OLD_HASH = "A" * 64
NEW_HASH = "B" * 64


class D1SchemaTests(unittest.TestCase):
    def setUp(self):
        self.db = sqlite3.connect(":memory:")
        self.db.executescript(MIGRATION.read_text(encoding="utf-8"))
        self.db.execute(
            """INSERT INTO refresh_sessions
              (id,user_id,token_hash,family_id,created_at,expires_at)
              VALUES (?,?,?,?,?,?)""",
            (OLD_ID, USER_ID, OLD_HASH, FAMILY_ID, NOW, NOW + 3600),
        )
        self.db.commit()

    def tearDown(self):
        self.db.close()

    def test_unique_hash_and_expiry_check(self):
        with self.assertRaises(sqlite3.IntegrityError):
            self.db.execute(
                """INSERT INTO refresh_sessions
                   (id,user_id,token_hash,family_id,created_at,expires_at)
                   VALUES (?,?,?,?,?,?)""",
                (NEW_ID, USER_ID, OLD_HASH, FAMILY_ID, NOW, NOW + 3600),
            )
        self.db.rollback()
        with self.assertRaises(sqlite3.IntegrityError):
            self.db.execute(
                """INSERT INTO refresh_sessions
                   (id,user_id,token_hash,family_id,created_at,expires_at)
                   VALUES (?,?,?,?,?,?)""",
                (NEW_ID, USER_ID, NEW_HASH, FAMILY_ID, NOW + 10, NOW),
            )
        self.db.rollback()

    def test_atomic_rotation_replay_does_not_reissue(self):
        sql_insert = (
            "INSERT INTO refresh_sessions "
            "(id,user_id,token_hash,family_id,created_at,expires_at) "
            "SELECT ?,user_id,?,family_id,?,? FROM refresh_sessions "
            "WHERE token_hash=? AND revoked_at IS NULL AND expires_at>?"
        )
        sql_revoke = (
            "UPDATE refresh_sessions SET revoked_at=?,replaced_by_token_id=? "
            "WHERE token_hash=? AND revoked_at IS NULL AND expires_at>?"
        )

        self.db.execute("BEGIN")
        inserted = self.db.execute(
            sql_insert, (NEW_ID, NEW_HASH, NOW+1, NOW+7200, OLD_HASH, NOW+1)
        ).rowcount
        revoked = self.db.execute(
            sql_revoke, (NOW+1, NEW_ID, OLD_HASH, NOW+1)
        ).rowcount
        self.db.commit()
        self.assertEqual((inserted, revoked), (1, 1))

        self.db.execute("BEGIN")
        inserted2 = self.db.execute(
            sql_insert, (NEW_ID, NEW_HASH, NOW+2, NOW+7200, OLD_HASH, NOW+2)
        ).rowcount
        revoked2 = self.db.execute(
            sql_revoke, (NOW+2, NEW_ID, OLD_HASH, NOW+2)
        ).rowcount
        self.db.commit()
        self.assertEqual((inserted2, revoked2), (0, 0))
        self.assertEqual(self.db.execute("SELECT COUNT(*) FROM refresh_sessions").fetchone()[0], 2)

    def test_indexes_exist(self):
        names = {
            row[0] for row in self.db.execute(
                "SELECT name FROM sqlite_master WHERE type='index'"
            )
        }
        self.assertIn("idx_refresh_sessions_user_expiry", names)
        self.assertIn("idx_refresh_sessions_family", names)
        self.assertIn("idx_session_audit_token_time", names)


if __name__ == "__main__":
    unittest.main()
