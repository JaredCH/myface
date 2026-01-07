# Content & User Behavior Controls (Primary Line of Defense)

## AI Agent Implementation Directives
- Objective: Build and enforce server-side content filtering, infractions, replacements, and layered rate limits (account/session/Tor heuristics) with admin-driven configuration.
- Non-negotiable: All filtering and replacements occur server-side before storage; no client JS reliance.
- Observability: Log every infraction with context and surface live metrics in Control Panel.

## 1. Content Filtering System (Control Panel Driven)
### 1.1 Word List Model (Backend)
Store a Word List table with:
- WordPattern: string (plain or regex; regex supported/flagged)
- MatchType: Exact | WordBoundary | Regex
- ActionType: InfractionAndMute | WordSwapOnly
- MuteDuration: null | 12h | 24h | 72h
- ReplacementText: nullable
- CaseSensitive: bool
- AppliesTo: flags (Threads, Comments, Chats)
- CreatedBy, CreatedAt, Enabled

### 1.2 Submission Flow (Admin Panel)
When adding a word:
1) Enter word/pattern
2) Choose action:
   - Infraction + mute (optional replacement)
   - Replacement only
3) If infraction: pick mute duration; optional replacement text
4) Select scopes (threads/comments/chat)

### 1.3 Infractions System
Log for every triggered match:
- UserId, ContentId, Word matched, Action taken, Timestamp, Expiry (mute), IP/session fingerprint (Tor-safe, see below)
Escalation rule (pick one and codify):
- Extend longest active mute **or** escalate 12 → 24 → 72 hours (no blind stacking).

### 1.4 Word Replacement Logic (No-JS)
- Filter server-side; replace before storage.
- Persist both:
  - Sanitized version (display)
  - Original version (admin-only, encrypted)
- Blocks: client bypass, replay, “edit to reveal” tricks.

### 1.5 Posting Rate Limits (Layered)
Base limits:
- Unverified: Threads 1/hour; Comments 6/hour
- PGP Verified: Threads 2/hour; Comments 15/hour
Tracking (Tor-aware):
- Account ID (primary)
- Session token (HTTP-only cookie)
- Tor circuit fingerprint heuristic: UA + Accept headers + TLS fingerprint hash + timing correlation
Cooldown handling:
- On exceed: accept request, delay 2–10s, return generic error or success-without-post. Avoid explicit “rate limited”.

## 2. Control Panel Requirements
- Word filter editor (CRUD, scopes, actions, durations, replacement text)
- Infraction log (user, word, action, mute expiry, fingerprints)
- Live metrics: actions/min, infractions/hour
- Rate limit overrides (temporary)
- Defense Mode toggle (Normal/Defense/Lockdown) displayed here

## Delivery Checklist (agent)
- Schema: add Word List + Infractions tables; backfill defaults disabled.
- Services: filtering, replacement, infractions, escalation, storage of sanitized+original.
- Middleware: enforce rate limits (account/session/heuristic) with silent failures.
- Admin UI: forms for word add/edit, scopes, actions; logs + live metrics.
- Tests: matching modes, escalation, replacement, rate-limit paths (normal and silent-fail).
