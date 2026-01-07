# Monitor Rollup Page Improvements

## Objective
Fix service removal bug and improve `/Monitor/Rollup` admin interface with better delete controls and add service functionality.

## Current Issues
- Removing services appears to succeed but doesn't actually delete
- No quick way to remove individual mirrors
- Missing add service feature on rollup page
- Deletion could be more intuitive

## Requirements

### Bug Fixes
1. **Service Removal**
   - Investigate why deletion doesn't persist
   - Check if service is being deleted from correct table/collection
   - Verify SaveChanges is called
   - Check for any soft-delete flags being set incorrectly

### UI Improvements
1. **Mirror Deletion**
   - Add small "×" icon next to each mirror link
   - Click opens confirmation: "Remove this mirror?"
   - Deletes only that specific mirror URL
   - Updates page without full reload if possible

2. **Service Deletion**
   - Add prominent "×" or "Delete Service" button on service card
   - Styled as warning/danger (red)
   - Confirmation dialog: "Delete [service name] and all its mirrors?"
   - Removes service and all associated mirrors

3. **Add Service**
   - Add "+ Add Service" button/section at top of page
   - Form fields:
     - Service Name
     - Primary URL
     - Category/Type
   - Submit adds to monitoring queue
   - Similar to old moderator page version

### Implementation Steps
1. Debug service removal - add logging, check database state
2. Fix deletion bug in service layer or controller
3. Add mirror delete endpoint (DELETE `/Monitor/RemoveMirror/{id}`)
4. Add service delete confirmation logic
5. Create add service form on rollup page
6. Add JavaScript for delete confirmations
7. Style delete buttons appropriately

### Files to Modify
- `Controllers/MonitorController.cs` - debug/fix deletion
- `Services/OnionMonitorService.cs` - verify delete logic
- `Views/Monitor/Rollup.cshtml` - add UI elements
- Potentially add JavaScript file for interactive deletes

### Testing
- Add test service, verify it appears
- Remove service, verify it's actually deleted from database
- Add mirrors to service, remove individual mirrors
- Check confirmation dialogs work
- Verify only admins can access this page
