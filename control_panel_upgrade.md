# Control Panel Upgrade Plan

## Phase 1 – Foundations & Access Control
**Status:** Completed - control panel shell, shared controller, and live metrics snapshot shipped on January 3, 2026.
- **Goals**: Unify moderator/admin panel entry points, enforce role gating, and prepare telemetry data pipelines required by later phases.
- **Tasks**:
  1. Inventory existing moderator/admin views and APIs; document ownership boundaries and authentication attributes.
  2. Introduce a shared `ControlPanelController` (and area routing) with role-based authorization attributes (`Moderator`, `Administrator`).
  3. Add a lightweight layout shell that matches the provided wireframe (header, nav, logout, role badge) without wiring metrics yet.
  4. Expand `VisitTrackingService` projections to expose reusable DTOs for live counts, peak stats, and rolling windows needed by multiple modules.
  5. Define a background refresh contract (SignalR-less push avoided; stick to zero-JS auto-refresh via `<meta refresh>` or server-triggered polling endpoints).
- **Deliverables**: Shared layout, nav scaffolding, consolidated controller entry, and documented data-service interfaces.

## Phase 2 – Operational Dashboard (Real-Time Overview)
**Status:** Completed – Live dashboard with role-aware widgets, infra collectors, and quick actions shipped on January 3, 2026.
- **Goals**: Ship the dashboard page with the live status, traffic summary, content velocity, quick actions, and system/security callouts described in the matrix.
- **Tasks**:
  1. Wire `VisitTrackingService` aggregates into dashboard view models for live users (15 min), active sessions (1 hr by session fingerprint), online users (4 hr) and peak concurrency (today).
  2. Surface page views (24h), new registrations, active threads, posts created, pending non-hidden reports, failed logins (1 hr), database size, disk, memory usage (admin-only subsections).
  3. Implement system metrics collectors (e.g., PostgreSQL `pg_database_size`, `df`, `/proc/meminfo`) behind an admin-only service with caching (>=60s) to avoid load.
  4. Build dashboard widgets per wireframe sections (Live Status, Traffic Summary, Content Velocity, Quick Actions).
  5. Quick Actions: hook up cache clear, metrics export, audit log view (admin-only) ensuring each action is logged to the audit trail.
- **Deliverables**: Fully functional dashboard view with role-trimmed data, refresh cadence controls (30s-5m live metrics, 1h storage refresh), and alert surfacing for abnormal states.

## Phase 3 – Traffic Analytics Module
**Status:** Completed – Cached analytics with CSV exports and moderator/admin data splits deployed on January 3, 2026.
- **Goals**: Provide moderators with non-sensitive flow metrics and admins with deeper behavioral analytics.
- **Tasks**:
  1. Extend `PageVisit` queries to compute top pages (24h), entry/exit pages (session grouping via `SessionFingerprint`), average session duration, and hourly traffic for the last 7 days.
  2. Add filters for anonymous vs authenticated visits and new vs returning users (admin-only toggle for referrers/bounce rate/new-vs-returning charts).
  3. Build aggregation jobs or SQL views to precompute heavy metrics (session duration, bounce rate) for responsive charts.
  4. Implement CSV export for admins while ensuring moderator views omit referrers and bounce-rate data as required.
- **Deliverables**: `/control-panel/traffic` page with charts/tables honoring role visibility.

## Phase 4 – Content Metrics & Media Monitoring
**Status:** Completed – Range filters, moderation stats, and media telemetry delivered on January 3, 2026.
- **Goals**: Measure forum velocity, moderation load, and media ingestion health.
- **Tasks**:
  1. Query `Threads`, `Posts`, `Votes`, and `Activity` to provide threads created per period, average posts per thread, most active categories, anonymous ratio, edits/deletes, sticky/pinned counts.
  2. Integrate moderation queues: reported content, moderation actions, and hide/unhide states.
  3. Add media telemetry: images uploaded (24h), upload failures, malware detections sourced from `UploadScanLog`.
  4. Provide filters by period (24h, 7d, 30d) and export options for admins.
- **Deliverables**: `/control-panel/content` section with role-aware widgets.

## Phase 5 – User Engagement Analytics
**Status:** Completed – Users dashboard with growth, contributors, suspensions, and PM telemetry deployed on January 3, 2026.
- **Goals**: Track growth, participation, trust, and inbox activity.
- **Tasks**:
  1. Combine `Users`, `Activity`, `Votes`, and `PrivateMessage` data to show total users, active users (7d), top contributors, most voted posts, suspended users list.
  2. Admin-only additions: user growth (30d chart), banned users, PGP-verified counts, private message volume (sent + drafts).
  3. Build drill-down cards linking to user detail overlays (login history, trust signals) while keeping read-only access logged.
- **Deliverables**: `/control-panel/users` engagement landing with actionable insights for moderators/admins.

## Phase 6 – Chat Monitoring & Security Dashboards
**Status:** Completed – Chat oversight controls and security telemetry (alerts, login spikes) deployed on January 3, 2026.
- **Goals**: Handle real-time chat oversight and security telemetry.
- **Tasks**:
  1. Leverage `ChatMessage` logs to show total messages, per-room breakdowns, hourly active chatters, verified-user percentages, mod/admin participation.
  2. Integrate `ChatService` mute/pause caches to show muted users, pause expirations, and enforcement controls (moderator caps: mute ≤24h, pause room ≤2h).
  3. Security metrics (admin-only): failed logins, rate-limited IP hashes, CAPTCHA challenge stats, enumeration attempts, password reset requests (add logging), session fingerprint change alerts.
  4. Visual alarm indicators for thresholds (e.g., spike in failed logins) with links to detailed logs.
- **Deliverables**: `/control-panel/chat` and `/control-panel/security` pages with respective role filtering.

## Phase 7 – Configuration Management (Dynamic Settings)
**Status:** Completed – DB-backed settings UI with caching, history, and runtime consumers deployed on January 3, 2026.
- **Goals**: Move hardcoded limits into DB-backed settings managed via the control panel (admin-only).
- **Tasks**:
  1. Design a `ControlSetting` entity / table storing key, value, metadata, and audit info.
  2. Externalize CAPTCHA thresholds (`CaptchaService`), rate-limit attempts/delays (`RateLimitService`), auto-refresh intervals, max upload size, session timeout, chat rate limits, post length caps, anonymous posting toggle (currently in `Program.cs`/`appsettings.json`).
  3. Build settings UI with validation, change history, and rollback snapshot support.
  4. Ensure runtime consumers (services/middleware) read through a caching layer with invalidation when settings change.
- **Deliverables**: Settings management UI + infrastructure with auditable change history.

## Phase 8 – User Management Controls & Audit Trail
**Status:** Completed – Manage User workspace with enforcement actions, history panels, and enriched audit logging deployed on January 3, 2026.
- **Goals**: Consolidate moderator/admin actions into a governed workflow with full logging.
- **Tasks**:
  1. Admin actions: ban/suspend, password reset, force username change, delete user, promote/demote moderator, view login history/IP hashes. Ensure destructive operations require confirmation, reason capture, and audit logging.
  2. Moderator actions: suspend (≤7 days), delete post (reason required), lock/unlock thread, hide reported content to queue, mute chat user (≤24h), pause room (≤2h). Enforce caps server-side.
  3. Expand the audit trail schema to capture who performed what, when, target entity, before/after values, and rationale text.
  4. Provide filtered audit log views plus export for admins (read-only access still logged).
- **Deliverables**: `/control-panel/users/manage` actions suite with shared components, guardrails, and auditable operations.

## Phase 9 – Deployment, QA, and Telemetry Validation
- **Goals**: Ensure each module is validated, documented, and monitored in production.
- **Tasks**:
  1. Add integration/permission tests per role for every control panel page/action.
  2. Document operational runbooks (refresh cadences, known limits, troubleshooting steps).
  3. Deploy incrementally (feature flags per phase), capture metrics vs. baseline, and adjust collectors for performance.
- **Deliverables**: Finalized documentation, automated tests, and production rollout with monitoring hooks.
