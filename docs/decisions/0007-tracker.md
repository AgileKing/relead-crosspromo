# 0007 — Tracker to api.relead.com

Date: 2026-11-27

- Events go to the **host game's** analytics: `POST https://api.relead.com/v1/games/{trackingGameId}/events`, `Authorization: Bearer <host's ingest key>` (the ingest key is public by design per the KOTGApi doc; the host passes it in config, it is not in this repo). `trackingGameId` defaults to `hostGameId`.
- File-backed queue (JSON lines in persistentDataPath), cap 1000 events (oldest dropped), batches ≤ 100 events / ≤ 200 KB (server: 100 / 256 KB incl. envelope). Auto-flush at 10 queued, plus after every show, at init and on app pause.
- **batch_id is kept across retries of the same batch** (stored in the queue file), so a retry after a lost 202 is deduplicated by the server. The doc says "per send attempt"; reusing it is what the server-side dedupe is for.
- 202 and 4xx → batch removed (contract). Exception: **408 and 429 are retried** — those are "later", not "never". 5xx/network → exponential backoff 30 s … 1 h (in memory; a restart retries right away).
- No key / endpoint / game id configured → events stay queued, nothing is sent.
