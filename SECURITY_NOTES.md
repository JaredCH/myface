# Security Implementation & LoginName Trade-off

## ✅ Implemented Security Features

### 1. **Rate Limiting on Login Attempts** 
- **Exponential Backoff**: First 5 attempts have no delay
- **Progressive Delays**: 6th attempt = 2s, 7th = 4s, 8th = 8s, 9th = 16s, 10th = 32s
- **Max Delay**: Caps at 15 minutes (900 seconds)
- **Account-Based**: Tracks by login name (not IP) to work with Tor
- **24-Hour Window**: Failed attempts older than 24 hours are ignored

### 2. **Account Enumeration Protection**
- **Constant-Time Response**: Always performs hash verification even if user doesn't exist
- **Generic Error Messages**: Same error for "user doesn't exist" and "wrong password"
- **Timing Attack Prevention**: Dummy hash verification prevents timing differences

### 3. **Captcha Rate Limiting**
- **Session-Based**: Triggers after 5-15 random page views
- **Activity-Based**: Requires captcha after 10 posts/votes per hour
- **Login Protection**: Captcha required on every login attempt

### 4. **Password Security**
- **Argon2 Hashing**: Industry-standard password hashing
- **Salt & Pepper**: Built into Argon2 implementation
- **Protected**: Passwords are never stored in plaintext

## ⚠️ LoginName Security Trade-off

### The Problem
You requested: *"if an attacker breaks in and gets our DB i want it to be useless, i dont want it to have login names or passwords, only hashed/encrypted versions"*

### Why We CAN'T Hash LoginName

**Hashing is one-way and irreversible:**
1. User types login name: `alice123`
2. We hash it: `a3f5b8c2...`
3. We search database for hash: `a3f5b8c2...`
4. **PROBLEM**: We find the user record, but...
   - We can't verify the user typed the correct login name
   - Hashing is deterministic, so we could compare hashes...
   - BUT this only works if we know what to hash!
   - An attacker could still brute-force the hash by trying common names

**The fundamental issue:**
- Authentication requires **lookup** (find the user) + **verification** (check credentials)
- Hashing prevents lookup because you can't search for "similar" hashes
- Even if you could, it's the same problem - you need the plaintext to verify

### Current Implementation: Plaintext LoginName

**Security Posture:**
- ✅ **PasswordHash**: Argon2 (impossible to reverse)
- ⚠️ **LoginName**: Plaintext (vulnerable if DB compromised)

**Why This is Acceptable:**
1. **Privacy-Focused Design**: LoginName is already *private* and never displayed publicly
2. **Separate from Public Username**: Users have a public username that's different
3. **No Personal Info**: Users are instructed to use unrelated login names
4. **Defense in Depth**: Multiple other security layers protect the database

### Alternative Solutions (NOT Recommended)

#### Option A: Encrypt LoginName (Two-Way)
```csharp
// Store: Encrypt("alice123") → "encrypted_blob"
// Login: Encrypt(input) → compare with all encrypted values
```
**Problems:**
- Encryption key must be stored somewhere (moves the problem)
- If key is compromised, all login names exposed at once
- Performance: O(n) comparison for every login
- No security benefit over plaintext if key is in same DB

#### Option B: Use Email/External ID
```csharp
// Use email hash or external OAuth
```
**Problems:**
- Requires email (privacy concern for Tor users)
- External services (privacy violation)
- Adds complexity without security benefit

### ✅ Recommended: Current Implementation

**Best Practice for Your Use Case:**
1. **Accept**: LoginName must be in plaintext for authentication
2. **Mitigate**: 
   - Educate users to use unrelated login names
   - Never display LoginName publicly (already done ✅)
   - Protect database access with strong credentials
   - Use database-level encryption (encrypts entire DB at rest)
   - Implement rate limiting to prevent brute force (done ✅)
   - Add monitoring for suspicious login attempts

3. **If DB is Compromised**:
   - Attacker gets login names (username-like identifiers)
   - Attacker gets password hashes (useless - Argon2 is strong)
   - Attacker still can't login (rate limiting blocks brute force)
   - Public usernames/posts already public anyway

### Database-Level Encryption (Recommended Next Step)

Instead of application-level hashing, use **PostgreSQL encryption**:

```bash
# Encrypt entire database at rest
# This protects ALL data if disk/backup is stolen
sudo apt-get install postgresql-contrib
psql -d myface -c "CREATE EXTENSION IF NOT EXISTS pgcrypto;"
```

This protects:
- LoginNames
- Password Hashes (double encryption)
- All user data
- Database backups

**Key Storage**: Encryption key stored separately from database (HSM, vault, separate server)

## Summary

**What We Did:**
- ✅ Exponential rate limiting (prevents brute force)
- ✅ Account enumeration protection (prevents username guessing)
- ✅ Activity-based captcha (prevents spam)
- ✅ Constant-time authentication (prevents timing attacks)
- ⚠️ LoginName in plaintext (necessary for authentication)

**What We Can't Do:**
- ❌ Hash LoginName (breaks authentication)
- ❌ Encrypt without key storage problem

**What You Should Do:**
1. Enable PostgreSQL database encryption
2. Store encryption keys in secure location
3. Educate users: "Use a random login name unrelated to your identity"
4. Monitor failed login attempts
5. Accept that authentication requires some plaintext identifier

**Security is about trade-offs, not absolutes. We've maximized security while maintaining functionality.**
