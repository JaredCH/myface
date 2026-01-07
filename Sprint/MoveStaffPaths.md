# Move Staff Pages to Secure Paths

## Objective
Consolidate all admin and moderator pages under `/SigilStaff/` path for better security and organization.

## Current Issues
- Inconsistent paths: some `/ControlPanel/`, some just `/`
- Path predictability could be security concern
- Harder to manage unified authentication/authorization

## Requirements

### Path Mapping
Current → New:
- `/ControlPanel` → `/SigilStaff/Traffic`
- `/ControlPanel/content` → `/SigilStaff/Content`
- `/ControlPanel/users` → `/SigilStaff/Users`
- `/ControlPanel/users/manage` → `/SigilStaff/Users/Manage`
- `/ControlPanel/chat` → `/SigilStaff/Chat`
- `/ControlPanel/security` → `/SigilStaff/Security`
- `/ControlPanel/wordlist` → `/SigilStaff/WordFilters`
- `/ControlPanel/infractions` → `/SigilStaff/Infractions`
- `/Monitor/Rollup` → `/SigilStaff/MonitorQueue`
- `/Moderator` or `/UserControl` → `/SigilStaff/UserControl`
- `/ControlPanel/settings` → `/SigilStaff/Settings`

### Implementation Steps
1. **Update Controller Routes**
   - Change `[Route("ControlPanel")]` to `[Route("SigilStaff")]`
   - Update all action route attributes
   - Keep method names unchanged for clarity

2. **Update Navigation Building**
   - Change all navigation URLs to new `/SigilStaff/` paths
   - Update BuildNavigation method

3. **Update View Links**
   - Search all views for old paths
   - Update asp-action, asp-controller, href attributes
   - Check RedirectToAction calls in controllers

4. **Update Form Actions**
   - Review all forms that post to control panel routes
   - Update asp-action attributes

5. **Add Route Redirects** (Optional but recommended)
   - Add redirects from old paths to new paths
   - Helps with bookmarks, external links
   - Return 301 Permanent Redirect

6. **Authorization Attributes**
   - Verify OnlyAdminAuthorization and OnlyModeratorAuthorization work
   - Consider adding authorization at controller level

### Files to Modify
- `Controllers/ControlPanelController.cs` - primary route changes
- `Controllers/MonitorController.cs` - rollup → monitor queue
- `Controllers/ModeratorController.cs` or new `UserControlController.cs`
- All views under `Views/ControlPanel/`
- Navigation building methods
- Any hardcoded links in layout files

### Testing Checklist
- All navigation links work
- Form submissions go to correct routes
- Breadcrumbs/page titles update correctly
- Old paths redirect or return 404
- Authorization still enforced
- Audit log references updated if needed
- Test as both Admin and Moderator

### Security Considerations
- Less predictable path than "ControlPanel"
- Easier to add additional middleware/filtering
- Can add path-based rate limiting
- Monitor logs for attempts to access old paths
