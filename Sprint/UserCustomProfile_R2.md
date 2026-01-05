# User Custom Profile System - Revision 2 (R2)

**Repository:** JaredCH/myface  
**Sprint Start Date:** 2026-01-05  
**Status:** Planning Phase  
**Current User:** JaredCH

---

## Overview

This document outlines the complete replacement of the current custom profile setup process with a new template-based system and secure custom HTML upload capability.  This is a living document - AI agents should check items as completed and add notes without overwriting existing content.

---

## System Architecture Summary

### New Profile System Components

1. **5 Standard Templates:** minimal, expanded, pro, vendor, Guru
2. **Theme/Color System:** Retain existing color choices, apply to templates
3. **Panel-Based Editing:** Users can edit/add/delete content in their template panels
4. **Custom HTML Option (BYOHTML):** Secure, sandboxed user-uploaded HTML
5. **Security Warning System:** Mandatory warning overlay for custom HTML profiles

### Security Model (Custom HTML)

- **Isolation:** Sandboxed iframe rendering only
- **No JavaScript:** Zero JS execution in user HTML
- **CSP Headers:** Strict Content Security Policy
- **Server-Side Sanitization:** C# with Ganss.XSS library
- **Network Isolation:** No external resources, Tor-compatible
- **Post-Validation:** Multi-layer verification after sanitization

---

## Phase 1: Discovery & Documentation

### Step 1.1: Audit Current Profile System ⬜

- [ ] Document all current profile customization features
- [ ] Identify all database tables/entities related to user profiles
- [ ] Map all controllers/views handling profile display and editing
- [ ] Document the current grid system implementation
- [ ] List all CSS/styling files for profiles
- [ ] Identify any JavaScript dependencies for profile rendering
- [ ] Document current security measures (if any)

**Files to investigate:**
- Controllers with "Profile" or "User" in name
- Views in user/profile directories
- Models/Entities for user profile data
- CSS files for grid/layout systems
- Any JavaScript for profile interactions

### Step 1.2: Audit Current Theme/Color System ⬜

- [ ] Document all available themes/colors
- [ ] Identify how themes are stored (database/config)
- [ ] Map theme application mechanism (CSS classes, variables, etc.)
- [ ] Verify themes work with new template designs
- [ ] Document any theme-related database schema

### Step 1.3: Design New Database Schema ⬜

**New entities/tables needed:**

- [ ] `ProfileTemplate` (enum or table): minimal, expanded, pro, vendor, Guru, custom_html
- [ ] `UserProfileSettings`: 
  - UserId (FK)
  - TemplateType (enum/FK)
  - ThemeId (FK to existing)
  - IsCustomHtml (bool)
  - CustomHtmlPath (string, nullable)
  - CustomHtmlUploadDate (datetime, nullable)
  - CustomHtmlValidated (bool)
- [ ] `ProfilePanel` (for template-based profiles):
  - PanelId (PK)
  - UserId (FK)
  - TemplateType (FK)
  - PanelType (enum:  about, skills, projects, contact, etc.)
  - Content (text)
  - Position (int, for ordering)
  - IsVisible (bool)
  - CreatedDate
  - ModifiedDate

**Migration strategy:**
- [ ] Plan data migration from old schema to new
- [ ] Identify default template for existing users
- [ ] Create rollback plan

---

## Phase 2: Backend Implementation - Core Infrastructure

### Step 2.1: Install Dependencies ⬜

- [ ] Add `HtmlSanitizer` (Ganss.XSS) NuGet package
- [ ] Verify version compatibility with current . NET version
- [ ] Document package version in this file

**Package:** `HtmlSanitizer by Ganss` - Version:  _[TO BE FILLED]_

### Step 2.2: Create New Models/Entities ⬜

- [ ] Create `ProfileTemplate` enum
- [ ] Create `UserProfileSettings` entity
- [ ] Create `ProfilePanel` entity
- [ ] Create `ProfilePanelType` enum
- [ ] Update DbContext with new DbSets
- [ ] Create database migrations
- [ ] Test migrations in development environment

**File locations:**
- Models: _[TO BE FILLED]_
- DbContext: _[TO BE FILLED]_
- Migrations: _[TO BE FILLED]_

### Step 2.3: Implement HTML Sanitization Service ⬜

Create `ICustomHtmlSanitizer` interface and `CustomHtmlSanitizer` implementation. 

**Requirements:**
- [ ] Use HtmlSanitizer library (Ganss.XSS)
- [ ] Configure allowlist for safe tags only
- [ ] Strip all dangerous attributes (on*, style, etc.)
- [ ] Block all external URLs (http, https, javascript:)
- [ ] Allow only relative paths and data: image/*
- [ ] Block SVG images entirely
- [ ] Implement post-sanitization validation
- [ ] Add node count limit (recommend:  1000 nodes max)
- [ ] Add output size limit (recommend: 500KB max)
- [ ] Return validation result with detailed errors

**Allowed Tags:**
```
div, span, p, br, hr, h1, h2, h3, h4, h5, h6, ul, ol, li, 
strong, b, em, i, u, blockquote, pre, code, table, thead, 
tbody, tr, th, td, img, a
```

**Forbidden Tags:**
```
script, style, iframe, object, embed, link, meta, base, 
svg, math, audio, video, canvas, form, input, button, 
select, textarea
```

**Allowed Attributes:**
- Global: `id, class, title, alt`
- `<a>`: `href` (forced `rel="nofollow noopener noreferrer"`)
- `<img>`: `src, alt, width, height`

**File location:** _[TO BE FILLED]_

### Step 2.4: Implement Custom HTML Storage Service ⬜

Create `ICustomHtmlStorageService` interface and implementation.

**Requirements:**
- [ ] Store sanitized HTML as static files
- [ ] Use path pattern:  `/u/{username}/profile.html`
- [ ] Store in isolated directory (e.g., `/wwwroot/user-html/`)
- [ ] Never store in main application directory
- [ ] Implement file size limits
- [ ] Add file versioning/backup on update
- [ ] Implement secure deletion
- [ ] Add audit logging for uploads

**File location:** _[TO BE FILLED]_

### Step 2.5: Implement Profile Template Service ⬜

Create `IProfileTemplateService` interface and implementation.

**Requirements:**
- [ ] CRUD operations for `UserProfileSettings`
- [ ] CRUD operations for `ProfilePanel`
- [ ] Get profile data with theme applied
- [ ] Validate panel content
- [ ] Apply theme to template
- [ ] Get available panels for each template type
- [ ] Reorder panels
- [ ] Toggle panel visibility

**File location:** _[TO BE FILLED]_

---

## Phase 3: Backend Implementation - Controllers & Endpoints

### Step 3.1: Create Profile Template Controller ⬜

**Endpoints needed:**

- [ ] `GET /profile/settings` - Get current user's profile settings
- [ ] `POST /profile/template/select` - Select a template (minimal/expanded/pro/vendor/Guru)
- [ ] `GET /profile/panels` - Get all panels for current user's template
- [ ] `POST /profile/panel/create` - Add a new panel
- [ ] `PUT /profile/panel/{id}` - Update panel content
- [ ] `DELETE /profile/panel/{id}` - Delete a panel
- [ ] `POST /profile/panel/{id}/reorder` - Change panel position
- [ ] `POST /profile/panel/{id}/toggle` - Show/hide panel
- [ ] `POST /profile/theme/apply` - Apply a theme/color

**Authorization:** All endpoints require authenticated user, can only modify own profile

**File location:** _[TO BE FILLED]_

### Step 3.2: Create Custom HTML Upload Controller ⬜

**Endpoints needed:**

- [ ] `POST /profile/custom-html/upload` - Upload custom HTML file
  - Accept . html file
  - Validate file size (max 500KB)
  - Call sanitization service
  - Store sanitized HTML
  - Update `UserProfileSettings`
  - Return validation results
  
- [ ] `GET /profile/custom-html/preview` - Preview sanitized HTML in sandbox
  - Serve with full CSP headers
  - Render in sandboxed iframe
  
- [ ] `DELETE /profile/custom-html` - Remove custom HTML, revert to template
  - Delete static file
  - Update `UserProfileSettings`
  
- [ ] `GET /profile/custom-html/status` - Get custom HTML upload status

**Authorization:** All endpoints require authenticated user, can only modify own profile

**File location:** _[TO BE FILLED]_

### Step 3.3: Create Profile Display Controller/Endpoint ⬜

**Endpoints needed:**

- [ ] `GET /u/{username}` - Display user's public profile
  - Check if user has custom HTML
  - If custom HTML:  render with iframe sandbox + warning
  - If template: render template with user's panels and theme
  
- [ ] `GET /u/{username}/profile. html` - Serve custom HTML (if exists)
  - Serve static file with strict CSP headers
  - No cookies, no auth headers
  - Apply all security headers

**CSP Headers for `/u/{username}/profile.html`:**
```
Content-Security-Policy:  default-src 'none'; img-src 'self' data:; style-src 'self'; font-src 'none'; media-src 'none'; frame-src 'none'; object-src 'none'; base-uri 'none'; form-action 'self';
X-Frame-Options: SAMEORIGIN
X-Content-Type-Options:  nosniff
Referrer-Policy: no-referrer
```

**File location:** _[TO BE FILLED]_

---

## Phase 4: Frontend Implementation - Templates & Views

### Step 4.1: Design Template HTML Structure ⬜

Create 5 template designs that maintain visual similarity to current design (side panels, center section) but without the old grid system.

**Templates to create:**

- [ ] **Minimal Template**
  - Single column, compact
  - Panels:  About, Contact
  - Use:  Simple flexbox layout
  
- [ ] **Expanded Template**
  - Two-column layout
  - Sidebar:  About, Skills
  - Main:  Projects, Activity, Contact
  - Use: Flexbox or CSS Grid
  
- [ ] **Pro Template**
  - Three-column layout
  - Left sidebar: Profile summary
  - Center: Main content panels
  - Right sidebar: Skills, Links
  - Use: CSS Grid
  
- [ ] **Vendor Template**
  - Business-focused layout
  - Header: Company info
  - Featured products/services section
  - Contact and testimonials
  - Use: CSS Grid
  
- [ ] **Guru Template**
  - Expert/influencer layout
  - Large header with bio
  - Content showcase
  - Social proof section
  - Use: CSS Grid

**Design principles:**
- Maintain visual similarity to current design
- Reuse existing panel styling
- Use modern CSS (flexbox/grid) instead of old grid system
- Mobile-responsive
- Theme/color compatible

**File locations:** _[TO BE FILLED]_

### Step 4.2: Create Template Partial Views ⬜

- [ ] Create Razor partial views for each template
- [ ] Create reusable panel components
- [ ] Implement theme/color application via CSS classes
- [ ] Create mobile-responsive breakpoints
- [ ] Test with existing color themes

**File locations:** _[TO BE FILLED]_

### Step 4.3: Create Profile Settings/Editor Views ⬜

**Views needed:**

- [ ] Template selection page (show 5 template previews + custom HTML option)
- [ ] Panel editor page (WYSIWYG or markdown editor for panel content)
- [ ] Theme/color selector (reuse existing if possible)
- [ ] Custom HTML upload page with instructions
- [ ] Preview modal for changes

**File locations:** _[TO BE FILLED]_

### Step 4.4: Create Custom HTML Profile View ⬜

**Requirements:**

- [ ] Create view that embeds iframe with user's custom HTML
- [ ] Implement prominent security warning overlay/banner
- [ ] Warning text:  "⚠️ This profile contains user-supplied HTML and is not endorsed.  Be suspicious of any phishing attempts, bold claims, or exaggerated promises."
- [ ] Warning must be visible at top of page
- [ ] Warning cannot be hidden by user
- [ ] Iframe must use sandbox attribute:  `sandbox="allow-same-origin"`
- [ ] Iframe must NOT allow:  scripts, top-navigation, popups, downloads, modals
- [ ] Add loading="lazy" and referrerpolicy="no-referrer"

**Iframe example:**
```html
<iframe
  src="/u/@Model.Username/profile.html"
  sandbox="allow-same-origin"
  referrerpolicy="no-referrer"
  loading="lazy"
  style="width: 100%; height:  100vh; border: none;">
</iframe>
```

**File location:** _[TO BE FILLED]_

---

## Phase 5: CSS & Styling

### Step 5.1: Preserve Existing Theme/Color System ⬜

- [ ] Audit all theme-related CSS
- [ ] Ensure themes apply to new templates
- [ ] Create CSS variables for themes (if not already using)
- [ ] Test each theme with each template
- [ ] Document any theme incompatibilities

**File locations:** _[TO BE FILLED]_

### Step 5.2: Create Template-Specific CSS ⬜

- [ ] Create CSS for minimal template
- [ ] Create CSS for expanded template
- [ ] Create CSS for pro template
- [ ] Create CSS for vendor template
- [ ] Create CSS for Guru template
- [ ] Ensure responsive design for all templates
- [ ] Maintain visual similarity to old design (panel styling, spacing)

**File location:** _[TO BE FILLED]_

### Step 5.3: Create Panel Component CSS ⬜

- [ ] Preserve existing panel styling (if it works well)
- [ ] Create reusable panel CSS classes
- [ ] Add edit/delete button styling for authenticated users
- [ ] Add drag-and-drop styling (if implementing reordering UI)

**File location:** _[TO BE FILLED]_

### Step 5.4: Create Custom HTML Warning Banner CSS ⬜

- [ ] Prominent warning banner styling
- [ ] High contrast, attention-grabbing
- [ ] Sticky/fixed position at top
- [ ] Cannot be obscured by iframe content
- [ ] Mobile-responsive

**File location:** _[TO BE FILLED]_

---

## Phase 6: Remove Old System (CAREFUL!)

### Step 6.1: Identify Safe-to-Remove Components ⬜

**DO NOT REMOVE:**
- [ ] Theme/color system (verified)
- [ ] Panel styling CSS (verified we're reusing)
- [ ] User authentication
- [ ] Any shared components

**SAFE TO REMOVE (after verification):**
- [ ] Old grid system CSS
- [ ] Old profile customization controllers/actions
- [ ] Old profile editor views
- [ ] Old profile data models (after migration)
- [ ] Old profile JavaScript (if any)

### Step 6.2: Create Data Migration Script ⬜

- [ ] Script to migrate existing user profiles to new system
- [ ] Assign default template (recommend: "expanded") to existing users
- [ ] Migrate existing profile data to new panel system
- [ ] Test migration on copy of production database
- [ ] Create rollback script

**File location:** _[TO BE FILLED]_

### Step 6.3: Deprecate Old Endpoints ⬜

- [ ] List all old profile-related endpoints
- [ ] Add deprecation notices
- [ ] Redirect to new endpoints where possible
- [ ] Plan removal date

### Step 6.4: Remove Old Code (FINAL STEP) ⬜

**Only after new system is fully tested:**

- [ ] Remove old grid system CSS (keep documented copy)
- [ ] Remove old profile controllers
- [ ] Remove old profile views
- [ ] Remove old profile models/entities (after successful migration)
- [ ] Remove old migrations (optional, can keep for history)
- [ ] Update documentation

---

## Phase 7: Testing & Security Validation

### Step 7.1: Unit Testing ⬜

- [ ] Test HTML sanitization service with malicious inputs
  - XSS attempts (`<script>alert('xss')</script>`)
  - Event handlers (`<img src=x onerror=alert(1)>`)
  - JavaScript protocols (`<a href="javascript:alert(1)">`)
  - Data exfiltration attempts
  - CSS injection attempts
  - SVG-based attacks
- [ ] Test profile template service (CRUD operations)
- [ ] Test custom HTML storage service
- [ ] Test URL validation (block external, allow relative)
- [ ] Test file size limits
- [ ] Test node count limits

**Test file location:** _[TO BE FILLED]_

### Step 7.2: Integration Testing ⬜

- [ ] Test complete profile creation flow (each template)
- [ ] Test custom HTML upload flow
- [ ] Test panel editing (add/edit/delete/reorder)
- [ ] Test theme application to all templates
- [ ] Test profile viewing (both template and custom HTML)
- [ ] Test security warning display on custom HTML profiles
- [ ] Test iframe sandboxing (verify no JS execution)
- [ ] Test CSP headers are applied correctly

### Step 7.3: Security Audit ⬜

**Verify all security layers:**

- [ ] Iframe sandbox prevents script execution
- [ ] Iframe sandbox prevents top-navigation
- [ ] Iframe sandbox prevents popups
- [ ] CSP headers block inline scripts
- [ ] CSP headers block external resources
- [ ] CSP headers block object/embed tags
- [ ] HTML sanitizer removes all script tags
- [ ] HTML sanitizer removes all event handlers
- [ ] HTML sanitizer blocks javascript:  protocol
- [ ] HTML sanitizer blocks data: URLs (except images)
- [ ] Post-validation catches any sanitization bypasses
- [ ] No external network requests possible from user HTML
- [ ] Warning banner is always visible
- [ ] Warning banner cannot be hidden via CSS injection

**Security testing checklist:**
- [ ] Attempt XSS via upload
- [ ] Attempt CSS injection
- [ ] Attempt clickjacking
- [ ] Attempt external resource loading
- [ ] Attempt iframe escape
- [ ] Attempt DOM clobbering
- [ ] Test with Tor Browser security levels (Standard, Safer, Safest)

### Step 7.4: Tor Compatibility Testing ⬜

- [ ] Test all templates in Tor Browser
- [ ] Verify no clearnet requests
- [ ] Verify no JavaScript dependencies
- [ ] Verify no external fonts
- [ ] Verify no external images
- [ ] Test at all Tor security levels
- [ ] Verify . onion URLs work correctly

### Step 7.5: User Acceptance Testing ⬜

- [ ] Test with sample users
- [ ] Gather feedback on template designs
- [ ] Test panel editing UX
- [ ] Test custom HTML upload UX
- [ ] Verify warning banner is clear and understandable
- [ ] Test mobile responsiveness

---

## Phase 8: Deployment

### Step 8.1: Pre-Deployment Checklist ⬜

- [ ] All tests passing
- [ ] Security audit completed
- [ ] Database migration script tested
- [ ] Rollback plan documented
- [ ] Backup of production database created
- [ ] Documentation updated
- [ ] User guide/help pages created

### Step 8.2: Staging Deployment ⬜

- [ ] Deploy to staging environment
- [ ] Run data migration on staging
- [ ] Verify all functionality in staging
- [ ] Test with real-like data
- [ ] Performance testing

### Step 8.3: Production Deployment ⬜

- [ ] Schedule maintenance window
- [ ] Notify users of changes
- [ ] Deploy to production
- [ ] Run data migration
- [ ] Monitor for errors
- [ ] Verify critical functionality
- [ ] Monitor performance

### Step 8.4: Post-Deployment ⬜

- [ ] Monitor error logs for 48 hours
- [ ] Gather user feedback
- [ ] Address critical issues immediately
- [ ] Plan for iterative improvements
- [ ] Update this document with lessons learned

---

## Configuration Reference

### HTML Sanitizer Configuration

```csharp
// Add to appsettings.json or configuration
{
  "CustomHtmlSettings": {
    "MaxFileSizeBytes": 512000,  // 500KB
    "MaxNodeCount": 1000,
    "AllowedTags": ["div", "span", "p", "br", "hr", "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "strong", "b", "em", "i", "u", "blockquote", "pre", "code", "table", "thead", "tbody", "tr", "th", "td", "img", "a"],
    "AllowDataImages": true,
    "AllowSvg": false,
    "AllowExternalUrls": false
  }
}
```

### CSP Header Configuration

```
Content-Security-Policy: default-src 'none'; img-src 'self' data:; style-src 'self'; font-src 'none'; media-src 'none'; frame-src 'none'; object-src 'none'; base-uri 'none'; form-action 'self';
X-Frame-Options: SAMEORIGIN
X-Content-Type-Options: nosniff
Referrer-Policy: no-referrer
X-XSS-Protection: 1; mode=block
```

---

## Notes & Discoveries

### [2026-01-05] - Initial Document Creation
- Document created by AI agent
- Repository: JaredCH/myface
- Primary languages: C# (57. 1%), HTML (30.9%), CSS (11.7%)
- This is a major refactoring - expect 2-4 week sprint depending on team size

### Agent Instructions
**TO FUTURE AI AGENTS WORKING ON THIS SPRINT:**

1. **ALWAYS read this document first** before making changes to the codebase
2. **DO NOT overwrite** existing content in this document
3. **CHECK OFF items** as you complete them using [x] instead of [ ]
4. **ADD NOTES** in the "Notes & Discoveries" section below with: 
   - Date
   - What you worked on
   - File locations you created/modified
   - Any issues encountered
   - Any deviations from the plan (with justification)
5. **UPDATE** the "File locations" placeholders as you create files
6. **DOCUMENT** any configuration changes
7. **IF YOU ENCOUNTER ISSUES**, add a note and ask the user before proceeding with major changes

### Notes Section (Add new entries below)

---

<!-- 
TEMPLATE FOR NEW NOTES:

### [YYYY-MM-DD] - [Your Work Summary]
- **Phase/Step:** [e.g., Phase 2, Step 2. 3]
- **Files Modified/Created:**
  - path/to/file1.cs
  - path/to/file2.cshtml
- **Completed Tasks:**
  - [x] Task 1
  - [x] Task 2
- **Issues Encountered:** [description]
- **Decisions Made:** [any deviations from plan]
- **Next Steps:** [what should be done next]

---

-->

## Summary Progress Tracker

- [ ] Phase 1: Discovery & Documentation (0/3 steps)
- [ ] Phase 2: Backend Infrastructure (0/5 steps)
- [ ] Phase 3: Controllers & Endpoints (0/3 steps)
- [ ] Phase 4: Frontend Templates & Views (0/4 steps)
- [ ] Phase 5: CSS & Styling (0/4 steps)
- [ ] Phase 6: Remove Old System (0/4 steps)
- [ ] Phase 7: Testing & Security (0/5 steps)
- [ ] Phase 8: Deployment (0/4 steps)

**Total Progress:  0/32 major steps completed**

---

## Quick Reference Links

- **Repository:** https://github.com/JaredCH/myface
- **HtmlSanitizer Library:** https://github.com/mganss/HtmlSanitizer
- **CSP Reference:** https://developer.mozilla.org/en-US/docs/Web/HTTP/CSP
- **Iframe Sandbox:** https://developer.mozilla.org/en-US/docs/Web/HTML/Element/iframe#sandbox

---

**End of Document** - Last updated: 2026-01-05
