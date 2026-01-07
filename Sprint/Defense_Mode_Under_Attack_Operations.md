# Defense Mode (Anti-Spam / Under-Attack Operations)

## AI Agent Implementation Directives
- Objective: Implement tiered Defense modes with auto/Manual triggers, Tor-safe captchas, global governors, and write-amplification protections.
- States: NORMAL, DEFENSE (soft, auto/manual), LOCKDOWN (hard, manual only).
- Behavior: Degrade gracefully (throttle/queue/disable), do not break core reads for verified users.

## 1. Defense Mode States
- NORMAL: default rules.
- DEFENSE (soft): auto or manual trigger; cut rate limits ~50%, double captcha frequency, enforce thread creation cooldown globally, throttle anonymous actions.
- LOCKDOWN (hard): manual only; disable new threads; heavy comment throttling; disable new account creation; read-only for non-verified users.

## 2. Attack Detection Signals (auto-trigger DEFENSE when 2+ trip)
- Spikes in low-variance threads/comments
- Repeated word patterns
- Identical post timing / interval spam
- Account age vs activity mismatch
- Captcha fail→success ratio spikes
- Excessive POSTs with invalid data

## 3. Captcha Strategy (Tor-Compatible, No JS)
- Use HTML form-based captchas (image/text), rotate per request.
- In DEFENSE: captcha on thread/comment creation + login attempts.
- In LOCKDOWN: captcha every POST except login for verified users; double frequency (every N instead of 2N actions).

## 4. Global Rate Governor
- Site-wide ceilings: max threads/hour, max comments/minute.
- If exceeded: queue requests; drop overflow silently; log aggregates only (avoid per-request amplification).

## 5. Write Amplification Protection
- During DEFENSE/LOCKDOWN: disable notifications, search indexing, nonessential secondary writes; batch writes where possible.

## 6. Control Panel Requirements
- Toggle: Normal / Defense / Lockdown (manual)
- Live metrics: actions/min, captcha failures, infractions/hour
- Temporary overrides for rate limits
- Visibility of auto-trigger reasons (which signals tripped)

## Delivery Checklist (agent)
- Mode state machine with persistence; API + admin toggle.
- Detection service consuming signals → auto DEFENSE.
- Captcha middleware wired to mode + frequency rules.
- Global rate governor (queue/drop) independent of user limits.
- Feature flags to suppress secondary writes under stress.
- UI: mode toggle, metrics, trigger reasons, override controls.
- Tests: mode transitions, signal aggregation, governor queue/drop, captcha enforcement paths.
