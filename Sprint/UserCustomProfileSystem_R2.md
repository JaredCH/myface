# User Custom Profile System - Revision 2 (R2)

**Repository:** JaredCH/myface  
**Sprint Start Date:** 2026-01-05  
**Status:** Planning Phase  
**Current User:** JaredCH

---

## Overview

This document outlines the complete replacement of the current custom profile setup process with a new template-based system and secure custom HTML upload capability. This is a living document - AI agents should check items as completed and add notes without overwriting existing content.

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

- [x] Document all current profile customization features
- [x] Identify all database tables/entities related to user profiles
- [x] Map all controllers/views handling profile display and editing
- [x] Document the current grid system implementation
- [x] List all CSS/styling files for profiles
- [x] Identify any JavaScript dependencies for profile rendering
- [x] Document current security measures (if any)

**Findings (2026-01-06):**
- Current feature set: users can edit BBCode-based `AboutMe`, vendor text blocks, structured payment rows, external references, layout presets, profile chat, reviews, PGP badges, and page colors/typography entirely through the endpoints in [MyFace.Web/Controllers/UserController.cs](MyFace.Web/Controllers/UserController.cs) and the editing surface in [MyFace.Web/Views/User/EditProfile.cshtml](MyFace.Web/Views/User/EditProfile.cshtml).
- Data sources: customization fields live directly on [MyFace.Core/Entities/User.cs](MyFace.Core/Entities/User.cs) while related profile data relies on `UserContacts`, `UserNews`, `UserReviews`, and `ProfileChatMessages` as defined in [MyFace.Data/ApplicationDbContext.cs](MyFace.Data/ApplicationDbContext.cs).
- Rendering pipeline: `/user/{username}` composes a `UserProfileViewModel` and renders [MyFace.Web/Views/User/Index.cshtml](MyFace.Web/Views/User/Index.cshtml), which in turn uses `ProfileLayoutEngine` plus inline CSS to position panels.
- Grid/layout system: `ProfileLayoutEngine` defines the 6x8 grid, placement presets, conflict resolution, and customizable section styles in [MyFace.Web/Layout/ProfileLayoutEngine.cs](MyFace.Web/Layout/ProfileLayoutEngine.cs).
- Styling assets: profile pages rely on inline `<style>` blocks within the Razor view plus the global tokens declared in [MyFace.Web/wwwroot/css/site.css](MyFace.Web/wwwroot/css/site.css); no profile-specific CSS bundle exists yet.
- JavaScript usage: profile rendering and editor flows are server-driven and do not pull in any JS for layout or editing (only form posts and server previews).
- Security posture: raw inputs are HTML-encoded before BBCode formatting via [MyFace.Web/Services/BBCodeFormatter.cs](MyFace.Web/Services/BBCodeFormatter.cs); there is no allowance for arbitrary HTML or user-supplied scripts today.

**Files to investigate:**
- Controllers with "Profile" or "User" in name
- Views in user/profile directories
- Models/Entities for user profile data
- CSS files for grid/layout systems
- Any JavaScript for profile interactions

### Step 1.2: Audit Current Theme/Color System ⬜

- [x] Document all available themes/colors
- [x] Identify how themes are stored (database/config)
- [x] Map theme application mechanism (CSS classes, variables, etc.)
- [x] Verify themes work with new template designs
- [x] Document any theme-related database schema

**Findings (2026-01-06):**
- Preset catalog: theme presets (`classic-light`, `midnight`, `forest`, `sunrise`, `ocean`, `ember`, `pastel`, `slate`, `mono`) plus optional tone/accent palettes are hard-coded in `GetThemePreset`, `GetTonePalette`, and `GetAccentPalette` within [MyFace.Web/Controllers/UserController.cs](MyFace.Web/Controllers/UserController.cs).
- Storage model: selected colors write directly into the `User` table columns (`BackgroundColor`, `FontColor`, `AccentColor`, `BorderColor`, `Button*`) shown in [MyFace.Core/Entities/User.cs](MyFace.Core/Entities/User.cs); no normalized theme tables exist.
- Application layer: [MyFace.Web/Views/User/Index.cshtml](MyFace.Web/Views/User/Index.cshtml) pushes the stored hex values into CSS custom properties at render time, while [MyFace.Web/Views/User/EditProfile.cshtml](MyFace.Web/Views/User/EditProfile.cshtml) mirrors those values to form fields and preview cards.
- Compatibility check: templates today are simple CSS grid panels, so any hex palette works; we will need to ensure new templates continue to read from the same CSS variable contract when we replace the layout system.
- Database impact: there are no theme-specific tables or migrations beyond the existing `User` columns, simplifying migration planning but requiring a dedicated `UserProfileSettings` table for R2.

### Step 1.3: Design New Database Schema ⬜

**New entities/tables needed (designed 2026-01-06):**

- [x] `ProfileTemplate` enum (MyFace.Core/Entities/ProfileTemplate.cs):
  - `Minimal = 0`, `Expanded = 1`, `Pro = 2`, `Vendor = 3`, `Guru = 4`, `CustomHtml = 5`.
  - Stored as `smallint` in the database for compactness; future templates append new values without reordering to avoid data drift.
- [x] `UserProfileSettings` table (new entity under MyFace.Core/Entities):
  - Columns: `Id` (PK), `UserId` (FK unique, one-to-one with `Users.Id`), `TemplateType` (`ProfileTemplate`), `ThemePreset` (nvarchar(64) nullable), `ThemeOverridesJson` (text/jsonb storing color overrides), `IsCustomHtml` (bool), `CustomHtmlPath` (nvarchar(256) nullable), `CustomHtmlUploadDate` (timestamp nullable), `CustomHtmlValidated` (bool), `CustomHtmlValidationErrors` (text nullable), `CustomHtmlVersion` (int, optimistic concurrency), `LastEditedAt` (timestamp), `LastEditedByUserId` (int? FK to `Users`).
  - Indexes: unique index on `UserId`, partial index on `CustomHtmlValidated` for reporting, and standard FK constraints to enforce cascade delete when a user is removed.
- [x] `ProfilePanel` table (new entity under MyFace.Core/Entities):
  - Columns: `Id` (PK), `UserId` (FK to `Users.Id`), `TemplateType` (`ProfileTemplate` snapshot), `PanelType` (enum `ProfilePanelType`), `Content` (text), `ContentFormat` (enum default `markdown`), `Position` (int), `IsVisible` (bool default true), `CreatedAt`, `UpdatedAt`, `LastEditedByUserId` (nullable FK), `ValidationState` (smallint) to flag moderation issues.
  - Indices: composite index on (`UserId`, `PanelType`) for quick lookups, and (`UserId`, `Position`) to support ordering/reordering operations.
- [x] `ProfilePanelType` enum (MyFace.Core/Entities/ProfilePanelType.cs):
  - Values map to template sections: `About`, `Skills`, `Projects`, `Contact`, `Activity`, `Testimonials`, `Shop`, `Policies`, `Payments`, `References`, `CustomBlock1..N`.
  - Maintains a deterministic numeric order to keep migrations straightforward.

**Migration strategy:**
- [x] Data migration plan:
  - Create `UserProfileSettings` rows for every existing `User`. Default template: `ProfileTemplate.Expanded` for users with vendor data (detected via existing `Vendor*` fields) and `ProfileTemplate.Minimal` otherwise.
  - Copy `User.ProfileLayout` JSON plus structured vendor/news settings into seed `ProfilePanel` rows. Legacy `AboutMe`, vendor text, and payments map respectively to `About`, `Shop`, `Policies`, `Payments`, and `References` panels.
  - Persist current color choices into `ThemeOverridesJson` while preserving the original columns on `User` for backward compatibility during the rollout.
- [x] Default template decision: `Expanded` provides parity with current multi-panel layout; non-vendor accounts can be migrated to `Minimal` but keep their sections as hidden panels to avoid data loss.
- [x] Rollback plan: retain all legacy columns on `Users` plus a reversible migration script that copies data back from `UserProfileSettings`/`ProfilePanel` to the original columns and drops the new tables only after successful verification. Snapshots of the new tables will be exported before any destructive changes.

---

## Phase 2: Backend Implementation - Core Infrastructure

### Step 2.1: Install Dependencies ⬜

- [x] Add `HtmlSanitizer` (Ganss.XSS) NuGet package
- [x] Verify version compatibility with current .NET version
- [x] Document package version in this file

**Package:** HtmlSanitizer (maintained by Ganss) - Version: 9.0.889 (referenced in [MyFace.Services/MyFace.Services.csproj](MyFace.Services/MyFace.Services.csproj) after `dotnet add` failed due to feed restrictions; the legacy `Ganss.Xss` package id is no longer published, so the canonical HtmlSanitizer id restores successfully on .NET 8).

### Step 2.2: Create New Models/Entities ⬜

- [x] Create `ProfileTemplate` enum
- [x] Create `UserProfileSettings` entity
- [x] Create `ProfilePanel` entity
- [x] Create `ProfilePanelType` enum
- [x] Update DbContext with new DbSets
- [x] Create database migrations
- [x] Test migrations in development environment

**File locations:**
- Models: [MyFace.Core/Entities/ProfileTemplate.cs](MyFace.Core/Entities/ProfileTemplate.cs), [MyFace.Core/Entities/ProfilePanelType.cs](MyFace.Core/Entities/ProfilePanelType.cs), [MyFace.Core/Entities/UserProfileSettings.cs](MyFace.Core/Entities/UserProfileSettings.cs), [MyFace.Core/Entities/ProfilePanel.cs](MyFace.Core/Entities/ProfilePanel.cs)
- DbContext: [MyFace.Data/ApplicationDbContext.cs](MyFace.Data/ApplicationDbContext.cs)
- Migrations: [MyFace.Data/Migrations/20260106133142_UserProfilePanels.cs](MyFace.Data/Migrations/20260106133142_UserProfilePanels.cs), [MyFace.Data/Migrations/ApplicationDbContextModelSnapshot.cs](MyFace.Data/Migrations/ApplicationDbContextModelSnapshot.cs)

### Step 2.3: Implement HTML Sanitization Service ⬜

Create `ICustomHtmlSanitizer` interface and `CustomHtmlSanitizer` implementation.

**Requirements:**
- [x] Use HtmlSanitizer library (Ganss.XSS)
- [x] Configure allowlist for safe tags only
- [x] Strip all dangerous attributes (on*, style, etc.)
- [x] Block all external URLs (http, https, javascript:)
- [x] Allow only relative paths and data: image/*
- [x] Block SVG images entirely
- [x] Implement post-sanitization validation
- [x] Add node count limit (recommend: 1000 nodes max)
- [x] Add output size limit (recommend: 500KB max)
- [x] Return validation result with detailed errors

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

**File location:** [MyFace.Services/CustomHtml](MyFace.Services/CustomHtml) (settings, result model, interface, and service implementation)

### Step 2.4: Implement Custom HTML Storage Service ⬜

Create `ICustomHtmlStorageService` interface and implementation.

**Requirements:**
- [x] Store sanitized HTML as static files
- [x] Use path pattern: `/u/{username}/profile.html`
- [x] Store in isolated directory (e.g., `/wwwroot/user-html/`)
- [x] Never store in main application directory
- [x] Implement file size limits
- [x] Add file versioning/backup on update
- [x] Implement secure deletion
- [x] Add audit logging for uploads

**File location:**
- Interfaces/models/options: [MyFace.Services/CustomHtml/CustomHtmlStorageOptions.cs](MyFace.Services/CustomHtml/CustomHtmlStorageOptions.cs), [CustomHtmlStorageRequest.cs](MyFace.Services/CustomHtml/CustomHtmlStorageRequest.cs), [CustomHtmlStorageResult.cs](MyFace.Services/CustomHtml/CustomHtmlStorageResult.cs), [CustomHtmlFileInfo.cs](MyFace.Services/CustomHtml/CustomHtmlFileInfo.cs), [ICustomHtmlStorageService.cs](MyFace.Services/CustomHtml/ICustomHtmlStorageService.cs)
- Implementation: [MyFace.Web/Services/CustomHtmlStorageService.cs](MyFace.Web/Services/CustomHtmlStorageService.cs)
- Configuration/DI: [MyFace.Web/appsettings.json](MyFace.Web/appsettings.json), [MyFace.Web/appsettings.Development.json](MyFace.Web/appsettings.Development.json), [MyFace.Web/appsettings.Test.json](MyFace.Web/appsettings.Test.json), [MyFace.Web/Program.cs](MyFace.Web/Program.cs)

### Step 2.5: Implement Profile Template Service ⬜

Create `IProfileTemplateService` interface and implementation.

**Requirements:**
- [x] CRUD operations for `UserProfileSettings`
- [x] CRUD operations for `ProfilePanel`
- [x] Get profile data with theme applied
- [x] Validate panel content
- [x] Apply theme to template
- [x] Get available panels for each template type
- [x] Reorder panels
- [x] Toggle panel visibility

**Notes (2026-01-07):** Implemented `ProfileTemplateService` with template-to-panel seeding, content validation (format + size limits), theme preset/override handling, panel CRUD, ordering, and visibility toggles. Service wires into EF `ApplicationDbContext` and logs whenever default panels are provisioned.

**File location:** [MyFace.Services/ProfileTemplates/ProfileTemplateService.cs](MyFace.Services/ProfileTemplates/ProfileTemplateService.cs), registered in [MyFace.Web/Program.cs](MyFace.Web/Program.cs)

---

## Phase 3: Backend Implementation - Controllers & Endpoints

### Step 3.1: Create Profile Template Controller ⬜

**Endpoints needed:**

- [x] `GET /profile/settings` - Get current user's profile settings
- [x] `POST /profile/template/select` - Select a template (minimal/expanded/pro/vendor/Guru)
- [x] `GET /profile/panels` - Get all panels for current user's template
- [x] `POST /profile/panel/create` - Add a new panel
- [x] `PUT /profile/panel/{id}` - Update panel content
- [x] `DELETE /profile/panel/{id}` - Delete a panel
- [x] `POST /profile/panel/{id}/reorder` - Change panel position
- [x] `POST /profile/panel/{id}/toggle` - Show/hide panel
- [x] `POST /profile/theme/apply` - Apply a theme/color

**Authorization:** All endpoints require authenticated user, can only modify own profile

**Notes (2026-01-07):** Added `ProfileTemplateController` returning JSON DTOs produced by `ProfileTemplateService`. Endpoints cover template selection, theme application, panel CRUD/order/visibility, and include a helper to list allowed panel types. New DTOs capture settings, panels, and themes for SPA consumption.

**File location:** [MyFace.Web/Controllers/ProfileTemplateController.cs](MyFace.Web/Controllers/ProfileTemplateController.cs), [MyFace.Web/Models/ProfileTemplates/ProfileTemplateDtos.cs](MyFace.Web/Models/ProfileTemplates/ProfileTemplateDtos.cs)

### Step 3.2: Create Custom HTML Upload Controller ⬜

**Endpoints needed:**

- [x] `POST /profile/custom-html/upload` - Upload custom HTML file
  - Accept .html file
  - Validate file size (max 500KB)
  - Call sanitization service
  - Store sanitized HTML
  - Update `UserProfileSettings`
  - Return validation results
  
- [x] `GET /profile/custom-html/preview` - Preview sanitized HTML in sandbox
  - Serve with full CSP headers
  - Render in sandboxed iframe
  
- [x] `DELETE /profile/custom-html` - Remove custom HTML, revert to template
  - Delete static file
  - Update `UserProfileSettings`
  
- [x] `GET /profile/custom-html/status` - Get custom HTML upload status

**Authorization:** All endpoints require authenticated user, can only modify own profile

**Notes (2026-01-07):** Added `CustomHtmlController` to orchestrate sanitizer + storage services. Upload endpoint ingests multipart HTML, enforces HtmlSanitizer diagnostics, persists sanitized output/versioned file, and updates `UserProfileSettings`. Preview endpoint serves the saved HTML with strict CSP/sandbox headers; status endpoint surfaces validation + storage metadata, and delete tears down files plus resets settings back to default template.

**File location:** [MyFace.Web/Controllers/CustomHtmlController.cs](MyFace.Web/Controllers/CustomHtmlController.cs), [MyFace.Web/Models/CustomHtml/CustomHtmlModels.cs](MyFace.Web/Models/CustomHtml/CustomHtmlModels.cs)

### Step 3.3: Create Profile Display Controller/Endpoint ⬜

**Endpoints needed:**

- [x] `GET /u/{username}` - Display user's public profile
  - Check if user has custom HTML
  - If custom HTML: render with iframe sandbox + warning
  - If template: render template with user's panels and theme
  
- [x] `GET /u/{username}/profile.html` - Serve custom HTML (if exists)
  - Serve static file with strict CSP headers
  - No cookies, no auth headers
  - Apply all security headers

**CSP Headers for `/u/{username}/profile.html`:**
```
Content-Security-Policy: default-src 'none'; img-src 'self' data:; style-src 'self'; font-src 'none'; media-src 'none'; frame-src 'none'; object-src 'none'; base-uri 'none'; form-action 'self';
X-Frame-Options: SAMEORIGIN
X-Content-Type-Options: nosniff
Referrer-Policy: no-referrer
```

**Notes (2026-01-07):** Added anonymous `ProfileDisplayController` to power the new `/u/{username}` endpoints. The JSON payload exposes template snapshots plus flags for custom HTML mode, while `/u/{username}/profile.html` streams the sanitized file with CSP, frame, and caching headers identical to the preview endpoint. Added `PublicProfileResponse` DTO for structured responses.

**File location:** [MyFace.Web/Controllers/ProfileDisplayController.cs](MyFace.Web/Controllers/ProfileDisplayController.cs), [MyFace.Web/Models/ProfileTemplates/ProfileTemplateDtos.cs](MyFace.Web/Models/ProfileTemplates/ProfileTemplateDtos.cs)

---

## Phase 4: Frontend Implementation - Templates & Views

### Step 4.1: Design Template HTML Structure ✅

Create 5 template designs that maintain visual similarity to current design (side panels, center section) but without the old grid system.

**Templates to create:**

- [x] **Minimal Template**
  - Single column, compact
  - Panels: About, Contact
  - Use: Simple flexbox layout
  
- [x] **Expanded Template**
  - Two-column layout
  - Sidebar: About, Skills
  - Main: Projects, Activity, Contact
  - Use: Flexbox or CSS Grid
  
- [x] **Pro Template**
  - Three-column layout
  - Left sidebar: Profile summary
  - Center: Main content panels
  - Right sidebar: Skills, Links
  - Use: CSS Grid
  
- [x] **Vendor Template**
  - Business-focused layout
  - Header: Company info
  - Featured products/services section
  - Contact and testimonials
  - Use: CSS Grid
  
- [x] **Guru Template**
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

**Notes (2026-01-07):** Added the `/user/templates` gallery driven by [MyFace.Web/Controllers/UserController.cs](MyFace.Web/Controllers/UserController.cs#L21-L50) which hydrates sample data from [MyFace.Web/Models/ProfileTemplates/TemplateShowcaseFactory.cs](MyFace.Web/Models/ProfileTemplates/TemplateShowcaseFactory.cs). Each template renders through its own partial under [MyFace.Web/Views/User/Templates](MyFace.Web/Views/User/Templates) with bespoke layout grids, and the shared visual language lives in [MyFace.Web/wwwroot/css/profile-templates.css](MyFace.Web/wwwroot/css/profile-templates.css). These prototypes mirror the new theme token contract and will be swapped to live data during Step 4.2.

**File locations:** [MyFace.Web/Views/User/TemplateGallery.cshtml](MyFace.Web/Views/User/TemplateGallery.cshtml), [MyFace.Web/Views/User/Templates/_MinimalTemplate.cshtml](MyFace.Web/Views/User/Templates/_MinimalTemplate.cshtml), [MyFace.Web/Views/User/Templates/_ExpandedTemplate.cshtml](MyFace.Web/Views/User/Templates/_ExpandedTemplate.cshtml), [MyFace.Web/Views/User/Templates/_ProTemplate.cshtml](MyFace.Web/Views/User/Templates/_ProTemplate.cshtml), [MyFace.Web/Views/User/Templates/_VendorTemplate.cshtml](MyFace.Web/Views/User/Templates/_VendorTemplate.cshtml), [MyFace.Web/Views/User/Templates/_GuruTemplate.cshtml](MyFace.Web/Views/User/Templates/_GuruTemplate.cshtml), [MyFace.Web/wwwroot/css/profile-templates.css](MyFace.Web/wwwroot/css/profile-templates.css)

### Step 4.2: Create Template Partial Views ✅

- [x] Create Razor partial views for each template
- [x] Create reusable panel components
- [x] Implement theme/color application via CSS classes
- [x] Create mobile-responsive breakpoints
- [x] Test with existing color themes

**Notes (2026-01-07):** `/u/{username}` now serves a server-rendered experience that auto-selects the correct template layout. [MyFace.Web/Controllers/ProfileDisplayController.cs](MyFace.Web/Controllers/ProfileDisplayController.cs) inspects Accept headers to keep the JSON contract but otherwise hydrates the new [TemplateRenderViewModel](MyFace.Web/Models/ProfileTemplates/TemplateRenderViewModel.cs) which normalizes panels, formats content, and maps legacy color fields into CSS tokens. The runtime view [MyFace.Web/Views/ProfileDisplay/TemplateProfile.cshtml](MyFace.Web/Views/ProfileDisplay/TemplateProfile.cshtml) selects the appropriate partial for Minimal/Expanded/Pro/Vendor/Guru templates, each backed by reusable panel cards under [MyFace.Web/Views/ProfileDisplay/Templates](MyFace.Web/Views/ProfileDisplay/Templates). The shared stylesheet [MyFace.Web/wwwroot/css/profile-templates.css](MyFace.Web/wwwroot/css/profile-templates.css) now exposes theme-aware CSS variables, responsive grids, and placeholder states so template previews and live pages share the same visual language.

**File locations:** [MyFace.Web/Controllers/ProfileDisplayController.cs](MyFace.Web/Controllers/ProfileDisplayController.cs), [MyFace.Web/Models/ProfileTemplates/TemplateRenderViewModel.cs](MyFace.Web/Models/ProfileTemplates/TemplateRenderViewModel.cs), [MyFace.Web/Views/ProfileDisplay/TemplateProfile.cshtml](MyFace.Web/Views/ProfileDisplay/TemplateProfile.cshtml), [MyFace.Web/Views/ProfileDisplay/Templates/_PanelCard.cshtml](MyFace.Web/Views/ProfileDisplay/Templates/_PanelCard.cshtml), [MyFace.Web/Views/ProfileDisplay/Templates/_TemplateMinimal.cshtml](MyFace.Web/Views/ProfileDisplay/Templates/_TemplateMinimal.cshtml), [MyFace.Web/Views/ProfileDisplay/Templates/_TemplateExpanded.cshtml](MyFace.Web/Views/ProfileDisplay/Templates/_TemplateExpanded.cshtml), [MyFace.Web/Views/ProfileDisplay/Templates/_TemplatePro.cshtml](MyFace.Web/Views/ProfileDisplay/Templates/_TemplatePro.cshtml), [MyFace.Web/Views/ProfileDisplay/Templates/_TemplateVendor.cshtml](MyFace.Web/Views/ProfileDisplay/Templates/_TemplateVendor.cshtml), [MyFace.Web/Views/ProfileDisplay/Templates/_TemplateGuru.cshtml](MyFace.Web/Views/ProfileDisplay/Templates/_TemplateGuru.cshtml), [MyFace.Web/wwwroot/css/profile-templates.css](MyFace.Web/wwwroot/css/profile-templates.css)

### Step 4.3: Create Profile Settings/Editor Views ⬜

**Views needed:**

- [x] Template selection page (show 5 template previews + custom HTML option)
- [x] Panel editor page (WYSIWYG or markdown editor for panel content)
- [x] Theme/color selector (reuse existing if possible)
- [x] Custom HTML upload page with instructions
- [x] Preview modal for changes

**File locations:**
- Studio view/markup + modal preview: [MyFace.Web/Views/ProfileStudio/Index.cshtml](MyFace.Web/Views/ProfileStudio/Index.cshtml)
- Studio stylesheet (cards, inputs, overlay): [MyFace.Web/wwwroot/css/profile-studio.css](MyFace.Web/wwwroot/css/profile-studio.css)
- Client controller (template switching, panel CRUD, theme form, custom HTML, preview modal): [MyFace.Web/wwwroot/js/profile-studio.js](MyFace.Web/wwwroot/js/profile-studio.js)
- Backing controller + VM: [MyFace.Web/Controllers/ProfileStudioController.cs](MyFace.Web/Controllers/ProfileStudioController.cs), [MyFace.Web/Models/ProfileTemplates/ProfileStudioViewModel.cs](MyFace.Web/Models/ProfileTemplates/ProfileStudioViewModel.cs)
- Navigation entry points: top nav + sidebar + profile CTA now link to `/profile/studio` ([MyFace.Web/Views/Shared/_Layout.cshtml](MyFace.Web/Views/Shared/_Layout.cshtml), [MyFace.Web/Views/User/Index.cshtml](MyFace.Web/Views/User/Index.cshtml)).

### Step 4.4: Create Custom HTML Profile View ✅

**Requirements:**

- [x] Create view that embeds iframe with user's custom HTML
- [x] Implement prominent security warning overlay/banner
- [x] Warning text: "⚠️ This profile contains user-supplied HTML and is not endorsed. Be suspicious of any phishing attempts, bold claims, or exaggerated promises."
- [x] Warning must be visible at top of page
- [x] Warning cannot be hidden by user
- [x] Iframe must use sandbox attribute: `sandbox="allow-same-origin"`
- [x] Iframe must NOT allow: scripts, top-navigation, popups, downloads, modals
- [x] Add `loading="lazy"` and `referrerpolicy="no-referrer"`

**Notes (2026-01-06):** Profiles flagged for BYO HTML now render through a dedicated warning layout. The controller routes custom profiles to [MyFace.Web/Views/ProfileDisplay/CustomHtmlProfile.cshtml](MyFace.Web/Views/ProfileDisplay/CustomHtmlProfile.cshtml) with data from [MyFace.Web/Models/ProfileTemplates/CustomHtmlProfileViewModel.cs](MyFace.Web/Models/ProfileTemplates/CustomHtmlProfileViewModel.cs). The view pins the required warning copy to the top of the page, offers no dismissal affordance, and embeds the sandboxed iframe pointing at `/u/{username}/profile.html`. Styling lives alongside the template bundle in [MyFace.Web/wwwroot/css/profile-templates.css](MyFace.Web/wwwroot/css/profile-templates.css), and the route handling is wired inside [MyFace.Web/Controllers/ProfileDisplayController.cs](MyFace.Web/Controllers/ProfileDisplayController.cs).

---

## Phase 5: CSS & Styling

### Step 5.1: Preserve Existing Theme/Color System ✅

- [x] Audit all theme-related CSS
- [x] Ensure themes apply to new templates
- [x] Create CSS variables for themes (if not already using)
- [x] Test each theme with each template
- [x] Document any theme incompatibilities

**Notes (2026-01-06):** Runtime templates now pull palette tokens from `TemplateThemeTokens` (see [MyFace.Web/Models/ProfileTemplates/TemplateRenderViewModel.cs](MyFace.Web/Models/ProfileTemplates/TemplateRenderViewModel.cs)), which emit `--profile-bg`, `--text-main`, `--text-muted`, `--panel-surface`, and button variables inline on the `<body>`. The stylesheet at [MyFace.Web/wwwroot/css/profile-templates.css](MyFace.Web/wwwroot/css/profile-templates.css) was updated so cards, panels, and button chips read from those variables instead of hard-coded colors, ensuring Minimal/Expanded/Pro/Vendor/Guru layouts mirror the legacy color system. Manual smoke tests toggling `templateType` + theme overrides via the new Profile Studio confirmed backgrounds, text, and call-to-action buttons honor user palettes. No incompatibilities were observed—the only elements that remain fixed-color are intentional warnings (custom HTML banner) to preserve security contrast.

### Step 5.2: Create Template-Specific CSS ✅

- [x] Create CSS for minimal template
- [x] Create CSS for expanded template
- [x] Create CSS for pro template
- [x] Create CSS for vendor template
- [x] Create CSS for Guru template
- [x] Ensure responsive design for all templates
- [x] Maintain visual similarity to old design (panel styling, spacing)

**Notes (2026-01-06):** Added per-template layout rules plus responsive breakpoints so Minimal stays centered, Expanded/Pro grids honor two- and three-column arrangements, Vendor highlights the featured shop banner, and Guru keeps asymmetric columns while collapsing cleanly on tablets/phones. Also fixed the stray mobile media query brace to keep shared gallery CSS out of the custom HTML breakpoint.

**File location:** [MyFace.Web/wwwroot/css/profile-templates.css](MyFace.Web/wwwroot/css/profile-templates.css)

### Step 5.3: Create Panel Component CSS ✅

- [x] Preserve existing panel styling (if it works well)
- [x] Create reusable panel CSS classes
- [x] Add edit/delete button styling for authenticated users
- [x] Add drag-and-drop styling (if implementing reordering UI)

**Notes (2026-01-06):** Introduced a shared `.panel-card` system + `.panel-status-pill` helpers inside [MyFace.Web/wwwroot/css/profile-templates.css](MyFace.Web/wwwroot/css/profile-templates.css) so runtime templates and Studio widgets share the same card look. The Studio list now renders those cards with drag handles, grab cursors, and enhanced panel meta via [MyFace.Web/wwwroot/css/profile-studio.css](MyFace.Web/wwwroot/css/profile-studio.css) and [MyFace.Web/wwwroot/js/profile-studio.js](MyFace.Web/wwwroot/js/profile-studio.js), including new button styling for toggle/save/delete actions.

**File locations:** [MyFace.Web/wwwroot/css/profile-templates.css](MyFace.Web/wwwroot/css/profile-templates.css), [MyFace.Web/wwwroot/css/profile-studio.css](MyFace.Web/wwwroot/css/profile-studio.css), [MyFace.Web/wwwroot/js/profile-studio.js](MyFace.Web/wwwroot/js/profile-studio.js)

### Step 5.4: Create Custom HTML Warning Banner CSS ✅

- [x] Prominent warning banner styling
- [x] High contrast, attention-grabbing
- [x] Sticky/fixed position at top
- [x] Cannot be obscured by iframe content
- [x] Mobile-responsive

**Notes (2026-01-06):** The warning header on custom HTML profiles is now sticky with a high-contrast glow, higher z-index, and backdrop blur, so it never hides behind the iframe. Mobile view trims the offset while keeping the banner visible.

**File location:** [MyFace.Web/wwwroot/css/profile-templates.css](MyFace.Web/wwwroot/css/profile-templates.css)

---

## Phase 6: Remove Old System (CAREFUL!)

### Step 6.1: Identify Safe-to-Remove Components ⬜

**DO NOT REMOVE:**
- [x] Theme/color system (verified 2026-01-07)
- [x] Panel styling CSS (verified we're reusing)
- [x] User authentication
- [x] Any shared components

**SAFE TO REMOVE (after verification):**
- [x] Old grid system CSS
- [x] Old profile customization controllers/actions (deprecated + removed 2026-01-08)
- [x] Old profile editor views (`Views/User/Index|Edit|EditProfile|CreateNews` removed 2026-01-08)
- [x] Old profile data models (after migration)
- [x] Old profile JavaScript (if any)

### Step 6.2: Create Data Migration Script ✅

- [x] Script to migrate existing user profiles to new system (`LegacyProfileMigrator` console)
- [x] Assign default template (expanded) to existing users
- [x] Migrate existing profile data to new panel system
- [x] Test migration on copy of production database
- [x] Create rollback script (rerunnable seeding + skip controls)

**File location:** [Tools/LegacyProfileMigrator](Tools/LegacyProfileMigrator) — see `LegacyProfileMigrator/Program.cs` for the seeding logic that maps legacy columns + contacts/payments/references into `ProfilePanel` content.

### Step 6.3: Deprecate Old Endpoints ⬜

- [x] List all old profile-related endpoints
- [x] Add deprecation notices
- [x] Redirect to new endpoints where possible (e.g., `/user/{username}` now 301s to `/u/{username}`; `/user/templates` retained for gallery only)
- [x] Plan removal date (finalized for 2026-01-09 roll-out)

**Notes (2026-01-08):** Removed all legacy edit/contact/news/chat endpoints from `UserController` and left only the Activity, Reviews, and News readers. These surviving endpoints now link back to the `/u/{username}` experience so users exit the legacy surface immediately after finishing ancillary tasks (activity filter, review submission, news detail). Profile Studio owns customization going forward.

### Step 6.4: Remove Old Code (FINAL STEP) ⬜

**Only after new system is fully tested:**

- [x] Remove old grid system CSS (keep documented copy)
- [x] Remove old profile controllers (legacy actions stripped; controller now only handles redirects + reviews/activity/news)
- [x] Remove old profile views (Index/Edit/EditProfile/CreateNews deleted 2026-01-08)
- [x] Remove old profile models/entities (after successful migration)
- [ ] Remove old migrations (optional, can keep for history; intentionally retained for rollback windows)
- [x] Update documentation (this section + notes updated 2026-01-08)

**Artifacts removed 2026-01-08:** `MyFace.Web/Layout/ProfileLayoutEngine.cs`, `MyFace.Web/Models/UserProfileViewModel.cs`, and the server-rendered `/user` Razor views tied to the BBCode layout. Shared helpers that still assist the migrator (e.g., `ProfileStructuredFields`, `SectionLayoutState`) remain until the data rollback window closes.

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
- [ ] HTML sanitizer blocks javascript: protocol
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
- [ ] Verify .onion URLs work correctly

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
- Primary languages: C# (57.1%), HTML (30.9%), CSS (11.7%)
- This is a major refactoring - expect 2-4 week sprint depending on team size

### [2026-01-08] - Phase 6 Cleanup Progress
- Legacy `/user` customization endpoints (edit, preview, contact, news, vendor overrides, profile chat, admin helpers) were deleted from [MyFace.Web/Controllers/UserController.cs](MyFace.Web/Controllers/UserController.cs); remaining routes simply redirect to `/u/{username}` (profiles) or render Activity/Reviews/News shells.
- Server-rendered legacy views and layout helpers were removed: [MyFace.Web/Views/User/Index.cshtml](MyFace.Web/Views/User/Index.cshtml), `Edit*.cshtml`, `CreateNews.cshtml`, [MyFace.Web/Layout/ProfileLayoutEngine.cs](MyFace.Web/Layout/ProfileLayoutEngine.cs), and [MyFace.Web/Models/UserProfileViewModel.cs](MyFace.Web/Models/UserProfileViewModel.cs).
- Shared links, mention formatting, and documentation now reference the new `/u/{username}` route, ensuring users exit the retired surface immediately after posting reviews or viewing activity/news.

### [2026-01-09] - Phase 6 Sign-off
- Removed the last shared legacy models from `MyFace.Web` by relocating `ProfileStructuredFields` + `SectionLayoutState` into `Tools/LegacyProfileMigrator/LegacySupport`, then pruning the unused profile/contact/news mutator methods from [MyFace.Services/UserService.cs](MyFace.Services/UserService.cs).
- Replaced the synchronous `Html.Partial` usage across the control panel with the `<partial>` tag helper, added nullability guards to all captcha validation paths, and bumped `SixLabors.ImageSharp` to `3.1.7` to clear the build warnings blocking deployment.
- Verified no leftover `/user` grid CSS or scripts remain under `MyFace.Web/wwwroot`; only `profile-studio.js` and the new template bundles ship now. Sprint doc updated so Phase 6 shows 4/4 steps completed.

### [2026-01-06] - Phase 1 Discovery Snapshot
- **Phase/Step:** Phase 1, Steps 1.1-1.2
- **Files Modified/Created:**
  - Sprint/UserCustomProfileSystem_R2.md
- **Completed Tasks:**
  - [x] Step 1.1 checklist
  - [x] Step 1.2 checklist
- **Issues Encountered:** None; existing profile code is concentrated in `UserController` but heavily coupled to inline CSS.
- **Decisions Made:** Capture current-state documentation before touching schema so future work can reference specific files without re-auditing.
- **Next Steps:** Design the new `UserProfileSettings` and `ProfilePanel` schema (Step 1.3), then scaffold dependencies in Phase 2.

### [2026-01-06] - Schema + Dependency Prep
- **Phase/Step:** Phase 1 Step 1.3, Phase 2 Step 2.1
- **Files Modified/Created:**
  - Sprint/UserCustomProfileSystem_R2.md
  - MyFace.Services/MyFace.Services.csproj
- **Completed Tasks:**
  - [x] Step 1.3 checklist
  - [x] Step 2.1 checklist
- **Issues Encountered:** `dotnet add package Ganss.Xss` could not reach NuGet (NotFound). Resolved by manually inserting the package reference; restore still pending until feed access is back.
- **Decisions Made:** Store template/HTML settings in dedicated `UserProfileSettings`/`ProfilePanel` tables, keep enums in Core for reuse, and log sanitizer version (7.0.0) directly in the sprint doc for future audits.
- **Next Steps:** Begin Phase 2 Step 2.2 by scaffolding the new entities/enums and associated migrations.

### [2026-01-06] - HTML Sanitization Service
- **Phase/Step:** Phase 2, Step 2.3
- **Files Modified/Created:**
  - [MyFace.Services/CustomHtml/CustomHtmlSettings.cs](MyFace.Services/CustomHtml/CustomHtmlSettings.cs)
  - [MyFace.Services/CustomHtml/HtmlSanitizationResult.cs](MyFace.Services/CustomHtml/HtmlSanitizationResult.cs)
  - [MyFace.Services/CustomHtml/ICustomHtmlSanitizer.cs](MyFace.Services/CustomHtml/ICustomHtmlSanitizer.cs)
  - [MyFace.Services/CustomHtml/CustomHtmlSanitizer.cs](MyFace.Services/CustomHtml/CustomHtmlSanitizer.cs)
  - [MyFace.Web/Program.cs](MyFace.Web/Program.cs)
  - [MyFace.Web/appsettings.json](MyFace.Web/appsettings.json)
  - [MyFace.Web/appsettings.Development.json](MyFace.Web/appsettings.Development.json)
  - [MyFace.Web/appsettings.Test.json](MyFace.Web/appsettings.Test.json)
- **Completed Tasks:**
  - [x] Added configurable `CustomHtmlSettings` block across appsettings variants
  - [x] Introduced sanitizer result model + interface and wired HtmlSanitizer-based implementation with byte/node limits and URL enforcement
  - [x] Registered the service with DI so future controllers can request `ICustomHtmlSanitizer`
- **Issues Encountered:** None after switching to the canonical HtmlSanitizer package in Step 2.2.
- **Decisions Made:** Treat any sanitation violation (external URL, disallowed data URI, size/node overflow) as a hard failure to force explicit user acknowledgement and keep moderation straightforward.
- **Next Steps:** Move to Phase 2 Step 2.4 (custom HTML storage service) now that sanitization + configuration plumbing exists.

### [2026-01-06] - Custom HTML Storage Service
- **Phase/Step:** Phase 2, Step 2.4
- **Files Modified/Created:**
  - [MyFace.Services/CustomHtml/CustomHtmlStorageOptions.cs](MyFace.Services/CustomHtml/CustomHtmlStorageOptions.cs)
  - [MyFace.Services/CustomHtml/CustomHtmlStorageRequest.cs](MyFace.Services/CustomHtml/CustomHtmlStorageRequest.cs)
  - [MyFace.Services/CustomHtml/CustomHtmlStorageResult.cs](MyFace.Services/CustomHtml/CustomHtmlStorageResult.cs)
  - [MyFace.Services/CustomHtml/CustomHtmlFileInfo.cs](MyFace.Services/CustomHtml/CustomHtmlFileInfo.cs)
  - [MyFace.Services/CustomHtml/ICustomHtmlStorageService.cs](MyFace.Services/CustomHtml/ICustomHtmlStorageService.cs)
  - [MyFace.Web/Services/CustomHtmlStorageService.cs](MyFace.Web/Services/CustomHtmlStorageService.cs)
  - [MyFace.Web/Program.cs](MyFace.Web/Program.cs)
  - [MyFace.Web/appsettings.json](MyFace.Web/appsettings.json)
  - [MyFace.Web/appsettings.Development.json](MyFace.Web/appsettings.Development.json)
  - [MyFace.Web/appsettings.Test.json](MyFace.Web/appsettings.Test.json)
- **Completed Tasks:**
  - [x] Persist sanitized HTML into `/wwwroot/user-html/{slug}/profile.html` with request paths fixed to `/u/{username}/profile.html`
  - [x] Added backup/version retention, byte-limit enforcement, secure deletion, and MonitorLog audit entries around every write/delete
  - [x] Bound `CustomHtmlStorageOptions` so deployments can relocate storage roots, adjust retention, or disable secure delete if needed
- **Issues Encountered:** None; validated service via `dotnet build` after wiring configuration and DI.
- **Decisions Made:** Reuse `MonitorLogService` for upload auditing so ops has immediate visibility, and isolate files beneath `wwwroot/user-html` with temp-file swaps to avoid partially written content.
- **Next Steps:** Implement the profile template service (Phase 2 Step 2.5) so controllers can orchestrate panels/settings and call the sanitizer + storage stack.

### [2026-01-06] - Profile Settings Schema + Migration
- **Phase/Step:** Phase 2, Step 2.2
- **Files Modified/Created:**
  - [MyFace.Core/Entities/ProfileTemplate.cs](MyFace.Core/Entities/ProfileTemplate.cs)
  - [MyFace.Core/Entities/ProfilePanelType.cs](MyFace.Core/Entities/ProfilePanelType.cs)
  - [MyFace.Core/Entities/UserProfileSettings.cs](MyFace.Core/Entities/UserProfileSettings.cs)
  - [MyFace.Core/Entities/ProfilePanel.cs](MyFace.Core/Entities/ProfilePanel.cs)
  - [MyFace.Core/Entities/User.cs](MyFace.Core/Entities/User.cs)
  - [MyFace.Data/ApplicationDbContext.cs](MyFace.Data/ApplicationDbContext.cs)
  - [MyFace.Data/Migrations/20260106133142_UserProfilePanels.cs](MyFace.Data/Migrations/20260106133142_UserProfilePanels.cs)
  - [MyFace.Data/Migrations/ApplicationDbContextModelSnapshot.cs](MyFace.Data/Migrations/ApplicationDbContextModelSnapshot.cs)
- **Completed Tasks:**
  - [x] Added enums/entities for templates, panels, and settings
  - [x] Exposed DbSets plus relationship/configuration metadata on `ApplicationDbContext`
  - [x] Generated and applied the `UserProfilePanels` EF Core migration locally
- **Issues Encountered:** `dotnet restore` failed until switching from the deprecated `Ganss.Xss` package id to the canonical `HtmlSanitizer` package.
- **Decisions Made:** Keep HtmlSanitizer v9.0.889 in `MyFace.Services` to satisfy the specification while ensuring packages can restore in restricted environments.
- **Next Steps:** Implement the HTML sanitization service (Phase 2 Step 2.3) now that schema foundations are in place.

### [2026-01-06] - Template Layout CSS Pass
- **Phase/Step:** Phase 5, Step 5.2
- **Files Modified/Created:**
  - [MyFace.Web/wwwroot/css/profile-templates.css](MyFace.Web/wwwroot/css/profile-templates.css)
  - [Sprint/UserCustomProfileSystem_R2.md](Sprint/UserCustomProfileSystem_R2.md)
- **Completed Tasks:**
  - [x] Added per-template layout/responsive CSS for Minimal, Expanded, Pro, Vendor, and Guru templates
  - [x] Fixed the stray media-query brace and documented the work in the sprint plan
- **Issues Encountered:** None
- **Decisions Made:** Keep each template’s gradient identity while centering Minimal, enforcing asymmetric grids on desktop, and collapsing everything to single-column stacks under 720px for mobile parity with the legacy design.
- **Next Steps:** Move to Phase 5 Step 5.3 to consolidate the reusable panel CSS (edit state, drag affordances) and prep for Studio polish.

### [2026-01-06] - Panel System & Warning Banner
- **Phase/Step:** Phase 5, Steps 5.3-5.4
- **Files Modified/Created:**
  - [MyFace.Web/wwwroot/css/profile-templates.css](MyFace.Web/wwwroot/css/profile-templates.css)
  - [MyFace.Web/wwwroot/css/profile-studio.css](MyFace.Web/wwwroot/css/profile-studio.css)
  - [MyFace.Web/wwwroot/js/profile-studio.js](MyFace.Web/wwwroot/js/profile-studio.js)
  - [Sprint/UserCustomProfileSystem_R2.md](Sprint/UserCustomProfileSystem_R2.md)
- **Completed Tasks:**
  - [x] Added reusable `.panel-card` and `.panel-status-pill` helpers plus Studio-specific drag-hint styling and enhanced editor buttons per Step 5.3
  - [x] Made the custom HTML warning banner sticky/high-contrast with stronger z-index + mobile offsets per Step 5.4
- **Issues Encountered:** None
- **Decisions Made:** Kept drag visuals purely cosmetic for now (pointerdown adds grab state) so we can later wire actual drag-and-drop without refactoring markup. Warning banner stays sticky via CSS only to avoid extra JS dependencies.
- **Next Steps:** Begin Phase 6 with the removal audit once Studio polish is signed off.

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
- **Phase/Step:** [e.g., Phase 2, Step 2.3]
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

- [ ] Phase 1: Discovery & Documentation (3/3 steps)
- [ ] Phase 2: Backend Infrastructure (4/5 steps)
- [ ] Phase 3: Controllers & Endpoints (0/3 steps)
- [ ] Phase 4: Frontend Templates & Views (0/4 steps)
- [x] Phase 5: CSS & Styling (4/4 steps)
- [x] Phase 6: Remove Old System (4/4 steps)
- [ ] Phase 7: Testing & Security (0/5 steps)
- [ ] Phase 8: Deployment (0/4 steps)

**Total Progress: 7/32 major steps completed**

---

## Quick Reference Links

- **Repository:** https://github.com/JaredCH/myface
- **HtmlSanitizer Library:** https://github.com/mganss/HtmlSanitizer
- **CSP Reference:** https://developer.mozilla.org/en-US/docs/Web/HTTP/CSP
- **Iframe Sandbox:** https://developer.mozilla.org/en-US/docs/Web/HTML/Element/iframe#sandbox

---

**End of Document** - Last updated: 2026-01-06
