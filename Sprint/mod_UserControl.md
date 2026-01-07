# Moderator User Control Page Redesign

## Objective
Rename `/Moderator` to `/UserControl` and redesign as a focused user moderation interface with pagination and enhanced controls.

## Current Issues
- Page shows too much information
- No pagination for large user lists
- Missing key moderation features (manual infractions, notes)
- Poor performance with many users

## Requirements

### Page Rename
- Change route from `/Moderator` to `/UserControl`
- Update all navigation links to point to new route

### Layout Changes
- Keep only the top section for user suspension management
- Remove extraneous sections (content stats, etc.)
- Clean, focused interface for user management

### Enhanced Features
1. **User Listing**
   - Pagination (20-50 users per page)
   - Default sort: Alphabetical by username
   - Optional sort: Registration date (newest/oldest)
   - Search/filter by username
   - Show: Username, Role, Status, Suspension, Infractions count, Last seen

2. **Moderation Actions** (per user)
   - Mute for custom timeframes (already exists - verify working)
   - **NEW: Manually apply infraction with note**
   - **NEW: Remove specific infraction with note**
   - Unmute user
   - View infraction history
   - Link to full user management page

3. **Manual Infraction Form**
   - User selector/input
   - Infraction reason (required text field)
   - Mute duration (optional dropdown)
   - Admin notes field
   - Creates UserInfraction record without tied content

4. **Remove Infraction**
   - List infractions with dates and reasons
   - Delete button with confirmation
   - Log removal in audit trail with note

### Implementation Steps
1. Rename controller action from `Moderator` to `UserControl`
2. Update route attributes
3. Add pagination logic (page parameter, skip/take)
4. Add sort parameter (username/date)
5. Create manual infraction form and POST action
6. Create remove infraction action
7. Update view with new layout and features
8. Update navigation links everywhere

### Files to Modify
- `Controllers/ControlPanelController.cs` or create `Controllers/UserControlController.cs`
- `Views/ControlPanel/Moderator.cshtml` → rename/recreate as `UserControl.cshtml`
- `Services/InfractionsService.cs` - add manual infraction method if needed
- All navigation references

### Testing
- Verify pagination works with large user list
- Test sorting by username and date
- Create manual infraction - verify it appears in user's record
- Remove infraction - verify deletion and audit log
- Check moderator permissions work correctly
