# Custom Error Configuration Plan

## ✅ INSPECTION COMPLETE - PARTIALLY APPLICABLE

**Date Completed:** January 5, 2026  
**Status:** ⚠️ Medium Risk - Error handling exists but could be improved  
**Reason:** ASP.NET Core application with basic error handling configured

---

## Inspection Findings

### ✅ Currently Implemented
- **UseExceptionHandler configured** - Program.cs line 150: `/Home/Error` for production
- **Generic error page exists** - Views/Shared/Error.cshtml shows generic message
- **No stack traces in production** - Development mode only
- **Environment-based behavior** - Different handling for dev vs. production

### ⚠️ Areas for Improvement
- **Missing Error action** - HomeController has no Error action method
- **No status code pages** - No specific handling for 404, 403, 500, etc.
- **Generic error page content** - Error.cshtml mentions "Development Mode" even in production view
- **No custom error pages** - All errors show same generic message
- **No structured logging** - Errors not systematically logged

### 📊 Risk Assessment
**Current Risk Level:** 🟡 MEDIUM
- Stack traces not exposed in production ✅
- Generic error messages prevent information disclosure ✅
- Missing specific error pages reduces user experience ⚠️
- No differentiation between error types ⚠️

---

## Recommended Correction Steps

### Priority: Medium - Improves security and UX

1. **Add Error action to HomeController**
   - Create `public IActionResult Error()` method
   - Return View with ErrorViewModel
   - Extract status code from features if available

2. **Configure status code pages middleware**
   - Add `app.UseStatusCodePagesWithReExecute("/Error/{0}")` after line 152 in Program.cs
   - Handles 404, 403, 500, etc. with custom pages

3. **Create specific error views**
   - Views/Shared/Error404.cshtml - "Page not found"
   - Views/Shared/Error403.cshtml - "Access denied"
   - Views/Shared/Error500.cshtml - "Server error"
   - Update Error action to route to specific views based on status code

4. **Update Error.cshtml for production**
   - Remove "Development Mode" section from production error page
   - Show only generic message and Request ID
   - Provide helpful links (home, contact, etc.)

5. **Add structured error logging**
   - Log exceptions with ILogger in controllers
   - Include Request ID, User ID (if authenticated), path
   - Never log sensitive data (passwords, tokens, etc.)

6. **Verify debug mode disabled in production**
   - Ensure ASPNETCORE_ENVIRONMENT is not "Development" in production
   - Verify appsettings.Production.json has appropriate logging levels

### Example Implementation

Add Error action to HomeController:
```csharp
public IActionResult Error()
{
    var statusCode = HttpContext.Response.StatusCode;
    
    return statusCode switch
    {
        404 => View("Error404"),
        403 => View("Error403"),
        500 => View("Error500"),
        _ => View(new ErrorViewModel 
        { 
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier 
        })
    };
}
```

Add to Program.cs after line 152:
```csharp
app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");
```

---

## Conclusion

**Action Required:** Recommended - Improves both security and user experience.

**Rationale:**
- Current implementation prevents information disclosure ✅
- Specific error pages improve user experience
- Better logging aids debugging without exposing details
- Status code handling makes errors more actionable

**Priority:** Medium - Should be implemented before production deployment

**Estimated Effort:** 4-6 hours (error pages + logging)
