# Monitor Link Rollup & Submission System (R0)

**Repository:** JaredCH/myface  
**Sprint Start Date:** 2026-01-05  
**Status:** Planning Phase  
**Current User:** JaredCH

---

## Overview

Consolidate duplicate monitor service links in the top 10 list, share click counts between mirrors, and implement a PGP-signed link submission system with admin approval.

---

## Problem Statement

- Multiple entries for same service (e.g., "Dark Matter", "dark matter", "DarkMatter")
- Mirror links and main links have separate click counts
- No way for users to submit new links
- Top 10 list shows duplicates instead of actual top services

---

## Solution Architecture

1. **Link Name Normalization** - Treat variations of same name as one entry
2. **Click Count Rollup** - Aggregate clicks across all variations and mirrors
3. **User Submission System** - PGP-signed link submissions with Title Case enforcement
4. **Admin Approval Queue** - Review and approve/deny submitted links

---

## Phase 1: Discovery & Audit

### Step 1.1: Audit Current Monitor/Link System ⬜
- [ ] Document database schema for links/monitors
- [ ] Identify how clicks are tracked and stored
- [ ] Document how top 10 list is generated
- [ ] Map controllers/views for link display
- [ ] Find the site-wide link banner component
- [ ] Document current link data (URLs, names, click counts)
- [ ] Export current top 10 list for analysis

**File locations:** _[TO BE FILLED]_

### Step 1.2: Identify Duplicate Services ⬜
- [ ] Query database for all link names
- [ ] Identify case variations (Dark Matter, dark matter, DARK MATTER)
- [ ] Identify spacing variations (Dark Matter, DarkMatter, Dark_Matter)
- [ ] Identify typos or legacy entries
- [ ] Create mapping of duplicates to canonical names
- [ ] Document mirror URLs for each service
- [ ] Estimate impact on top 10 rankings

**Duplicate mapping:** _[TO BE FILLED]_

### Step 1.3: Design Normalization Strategy ⬜
- [ ] Define canonical name format (Title Case)
- [ ] Create algorithm for matching similar names
- [ ] Decide: automatic consolidation or manual mapping?
- [ ] Plan for handling edge cases (similar but different services)
- [ ] Design database schema changes

---

## Phase 2: Database Schema Updates

### Step 2.1: Design New Schema ⬜
- [ ] Create `MonitorService` table (canonical services)
- [ ] Create `MonitorLink` table (URLs, variants, mirrors)
- [ ] Create `LinkClickLog` table (aggregate clicks)
- [ ] Create `LinkSubmission` table (pending submissions)
- [ ] Create `LinkNameVariant` table (track all name variations)
- [ ] Design relationships between tables
- [ ] Plan for preserving historical click data

**Schema notes:** _[TO BE FILLED]_

### Step 2.2: Create Migration Script ⬜
- [ ] Migrate existing links to new schema
- [ ] Create canonical entries for each service
- [ ] Map all name variations to canonical entries
- [ ] Aggregate historical click data
- [ ] Link mirror URLs to main entries
- [ ] Test migration on database copy
- [ ] Create rollback plan

### Step 2.3: Add New Fields ⬜
- [ ] Add `CanonicalName` field
- [ ] Add `NormalizedName` field (lowercase, no spaces)
- [ ] Add `IsMirror` boolean
- [ ] Add `MainLinkId` foreign key (for mirrors)
- [ ] Add `IsApproved` boolean
- [ ] Add `SubmittedBy` user reference
- [ ] Add `SubmittedDate`, `ApprovedDate`, `ApprovedBy`
- [ ] Add `PgpSignature` text field
- [ ] Add `VerificationStatus` enum

---

## Phase 3: Backend - Link Normalization

### Step 3.1: Create Link Normalization Service ⬜
- [ ] Normalize names (Title Case, trim spaces)
- [ ] Generate normalized comparison key
- [ ] Match similar names (fuzzy matching)
- [ ] Handle special characters
- [ ] Create canonical name from variations

### Step 3.2: Create Link Rollup Service ⬜
- [ ] Aggregate clicks across all name variations
- [ ] Aggregate clicks across mirror URLs
- [ ] Calculate total clicks per canonical service
- [ ] Generate top 10 list using rolled-up data
- [ ] Cache results for performance

### Step 3.3: Update Click Tracking ⬜
- [ ] Modify click tracking to use canonical ID
- [ ] Ensure all variations increment same counter
- [ ] Ensure mirror clicks count toward main service
- [ ] Update analytics/reporting

---

## Phase 4: Backend - PGP Submission System

### Step 4.1: Create PGP Verification Service ⬜
- [ ] Install PGP library (e.g., BouncyCastle)
- [ ] Parse PGP signed messages
- [ ] Extract signed content (URL)
- [ ] Verify signature validity
- [ ] Extract signer's key ID
- [ ] Validate .onion URL format
- [ ] Return verification result with details

### Step 4.2: Create Link Submission Service ⬜
- [ ] Validate service name (Title Case enforcement)
- [ ] Validate PGP block format
- [ ] Extract URL from signed message
- [ ] Verify PGP signature
- [ ] Check for duplicate submissions
- [ ] Check if service already exists
- [ ] Save to submission queue
- [ ] Send notification to admins

### Step 4.3: Create Admin Approval Service ⬜
- [ ] Retrieve pending submissions
- [ ] Approve submission (create live link)
- [ ] Deny submission (with reason)
- [ ] Bulk approve/deny
- [ ] Track approval history
- [ ] Notify submitter of decision

---

## Phase 5: Backend - Controllers & API

### Step 5.1: Create Link Submission Controller ⬜
- [ ] POST /links/submit - Submit new link
- [ ] GET /links/submit - Show submission form
- [ ] Validate input
- [ ] Enforce authentication (users only)
- [ ] Rate limit submissions
- [ ] Return validation errors

### Step 5.2: Create Admin Link Management Controller ⬜
- [ ] GET /admin/links/pending - View submission queue
- [ ] GET /admin/links/pending/{id} - View single submission
- [ ] POST /admin/links/approve/{id} - Approve submission
- [ ] POST /admin/links/deny/{id} - Deny submission
- [ ] GET /admin/links/duplicates - View potential duplicates
- [ ] POST /admin/links/merge - Merge duplicate entries
- [ ] Authorization: Admin role only

### Step 5.3: Update Link Display Logic ⬜
- [ ] Modify top 10 query to use rolled-up data
- [ ] Ensure canonical names are displayed
- [ ] Show aggregated click counts
- [ ] Update link banner component
- [ ] Cache top 10 list (refresh every N minutes)

---

## Phase 6: Frontend - Link Submission

### Step 6.1: Create Submission Form ⬜
- [ ] Service name input (auto Title Case)
- [ ] PGP block textarea (monospace font)
- [ ] Submit button
- [ ] Validation feedback (real-time)
- [ ] Show PGP format example
- [ ] Success/error messages
- [ ] Link to PGP signing guide

**File location:** _[TO BE FILLED]_

### Step 6.2: Add Client-Side Validation ⬜
- [ ] Validate service name format
- [ ] Enforce Title Case
- [ ] Validate PGP block structure (BEGIN/END markers)
- [ ] Check for .onion URL
- [ ] Show inline error messages
- [ ] Disable submit until valid

### Step 6.3: Create Submission Confirmation Page ⬜
- [ ] Show submitted service name
- [ ] Show extracted URL (if verified)
- [ ] Show PGP verification status
- [ ] Show "pending admin approval" message
- [ ] Link back to monitor page

---

## Phase 7: Frontend - Admin Approval Interface

### Step 7.1: Create Pending Submissions List ⬜
- [ ] Table/list of pending submissions
- [ ] Show: service name, submitted by, date, status
- [ ] Filter: pending/approved/denied
- [ ] Sort by date
- [ ] Pagination
- [ ] Count of pending submissions

### Step 7.2: Create Submission Detail View ⬜
- [ ] Display service name (Title Case formatted)
- [ ] Display extracted .onion URL (clickable)
- [ ] Display PGP signature block (collapsible)
- [ ] Display verification status (valid/invalid)
- [ ] Display submitter username and date
- [ ] Display signer key ID
- [ ] Approve button (with confirmation)
- [ ] Deny button (with reason input)
- [ ] Test link button (opens in new tab)

### Step 7.3: Create Duplicate Management View ⬜
- [ ] List of potential duplicates
- [ ] Show similar names grouped together
- [ ] Show click counts for each variant
- [ ] Option to merge duplicates
- [ ] Select canonical name
- [ ] Preview merged result
- [ ] Confirm merge action

---

## Phase 8: Testing & Validation

### Step 8.1: Test Link Normalization ⬜
- [ ] Test Title Case enforcement
- [ ] Test with various name formats
- [ ] Test duplicate detection
- [ ] Test fuzzy matching accuracy
- [ ] Test edge cases (special characters, numbers)

### Step 8.2: Test Click Rollup ⬜
- [ ] Test click tracking on canonical service
- [ ] Test click tracking on name variations
- [ ] Test click tracking on mirror URLs
- [ ] Verify top 10 list accuracy
- [ ] Test with large click volumes

### Step 8.3: Test PGP Verification ⬜
- [ ] Test valid PGP signatures
- [ ] Test invalid signatures
- [ ] Test malformed PGP blocks
- [ ] Test with different PGP key types
- [ ] Test signature with wrong content
- [ ] Test with expired keys
- [ ] Test URL extraction accuracy

### Step 8.4: Test Submission Workflow ⬜
- [ ] Test submission as regular user
- [ ] Test submission with invalid data
- [ ] Test duplicate submission prevention
- [ ] Test rate limiting
- [ ] Test without authentication
- [ ] Test approval workflow
- [ ] Test denial workflow
- [ ] Test notifications

### Step 8.5: Test Admin Interface ⬜
- [ ] Test pending list display
- [ ] Test filtering and sorting
- [ ] Test detail view
- [ ] Test clickable test links
- [ ] Test approve action
- [ ] Test deny action
- [ ] Test without admin permissions
- [ ] Test bulk actions

### Step 8.6: Security Testing ⬜
- [ ] Test PGP signature spoofing attempts
- [ ] Test SQL injection in inputs
- [ ] Test XSS in service names
- [ ] Test CSRF protection
- [ ] Test authorization bypass attempts
- [ ] Test malicious .onion URLs
- [ ] Test rate limit bypass

---

## Phase 9: Data Migration & Cleanup

### Step 9.1: Clean Existing Data ⬜
- [ ] Identify all Dark Matter variations
- [ ] Merge Dark Matter entries
- [ ] Identify other common duplicates
- [ ] Merge duplicates systematically
- [ ] Verify click counts are preserved
- [ ] Test top 10 list after cleanup

### Step 9.2: Link Mirrors to Main Entries ⬜
- [ ] Identify known mirror URLs
- [ ] Link mirrors to canonical entries
- [ ] Share click counts
- [ ] Update display logic
- [ ] Test mirror click tracking

### Step 9.3: Verify Top 10 Accuracy ⬜
- [ ] Generate new top 10 list
- [ ] Compare to old list
- [ ] Verify rankings make sense
- [ ] Document changes
- [ ] Spot check click counts

---

## Phase 10: Deployment

### Step 10.1: Pre-Deployment Checklist ⬜
- [ ] All tests passing
- [ ] Database migration tested
- [ ] Rollback plan documented
- [ ] Admin interface tested
- [ ] Documentation updated
- [ ] Backup database

### Step 10.2: Staging Deployment ⬜
- [ ] Deploy to staging
- [ ] Run migration
- [ ] Test submission flow
- [ ] Test admin approval
- [ ] Verify top 10 list
- [ ] Performance test

### Step 10.3: Production Deployment ⬜
- [ ] Deploy to production
- [ ] Run migration
- [ ] Verify top 10 list updates
- [ ] Monitor error logs
- [ ] Test submission
- [ ] Test approval

### Step 10.4: Post-Deployment ⬜
- [ ] Monitor submission queue
- [ ] Monitor for errors
- [ ] Gather user feedback
- [ ] Document issues
- [ ] Plan improvements

---

## Configuration

### PGP Settings
- Accepted hash algorithms: SHA256, SHA512
- Accepted key types: RSA, EdDSA
- URL format: Must be .onion domain
- Max signature size: 10KB

### Submission Rules
- Rate limit: 3 submissions per day per user
- Service name: 3-50 characters, Title Case
- Authentication: Required
- PGP: Must be validly signed

### Admin Settings
- Pending notification threshold: 5 submissions
- Auto-deny after: 30 days pending
- Submission history retention: 1 year

---

## Notes & Discoveries

### [2026-01-05] - Initial Document Creation
- Document created by AI agent
- Focus: Consolidate duplicate links, PGP submission system
- Example: Dark Matter has multiple entries in top 10

### Agent Instructions
1. Read this document first
2. Check off completed items [x]
3. Add notes below with date, files modified, issues
4. Update file location placeholders
5. Document all decisions and deviations

---

## Summary Progress

- [ ] Phase 1: Discovery & Audit (0/3)
- [ ] Phase 2: Database Schema (0/3)
- [ ] Phase 3: Link Normalization (0/3)
- [ ] Phase 4: PGP Submission (0/3)
- [ ] Phase 5: Controllers & API (0/3)
- [ ] Phase 6: Submission Frontend (0/3)
- [ ] Phase 7: Admin Interface (0/3)
- [ ] Phase 8: Testing (0/6)
- [ ] Phase 9: Data Migration (0/3)
- [ ] Phase 10: Deployment (0/4)

**Total: 0/34 steps completed**

---

**End of Document**
