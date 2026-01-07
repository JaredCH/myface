# Side Panel Navigation Uniformity

## Objective
Unify the left side panel navigation links across all staff pages to show consistent navigation while respecting role-based access.

## Current Issues
- Different pages show different navigation links
- Inconsistent user experience across control panel pages

## Requirements

### Navigation Structure
Show all relevant links consistently on every staff page:
- Traffic (Admin only)
- Content (Moderator+)
- Users (Moderator+)
- Chat Oversight (Moderator+)
- Security (Admin only)
- Word Filters (Admin only)
- Infractions (Admin only)
- Monitor Queue (Admin only)
- UserControl (Moderator+)
- Admin Panel (Admin only)
- Settings (Admin only)

### Implementation
1. Locate navigation building logic (likely in ControlPanelController or shared layout/partial)
2. Create unified navigation method that returns all links based on role
3. Apply role-based filtering:
   - Admins see all links
   - Moderators see: Content, Users, Chat Oversight, UserControl
   - Regular users see nothing (shouldn't access these pages)
4. Update all staff page views to use unified navigation
5. Ensure active page highlighting works consistently

### Files to Modify
- `Controllers/ControlPanelController.cs` - BuildNavigation method
- Any shared layout files used by control panel pages
- Individual view files that render navigation

### Testing
- Login as Admin - verify all links visible
- Login as Moderator - verify only moderator links visible
- Check navigation consistency across all pages
- Verify active page highlighting
