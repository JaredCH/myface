# Login Name / Username Separation - Implementation Summary

**Date:** December 24, 2025  
**Feature:** Decoupled private login credentials from public username display

---

## 🎯 Overview

Implemented a privacy-enhancing feature that separates:
- **Login Name** (private, used only for authentication)
- **Username** (public, displayed throughout the site)

This allows users to maintain anonymity by using different credentials for login vs. display.

---

## 📋 Changes Made

### 1. Database Schema Changes

**File:** `MyFace.Core/Entities/User.cs`
```csharp
public string LoginName { get; set; } // Private - authentication only
public string Username { get; set; }  // Public - displayed on site
```

**Migration:** `AddLoginNameToUser`
- Added `LoginName` column to Users table
- Migrated existing users: `LoginName = Username` (preserves current functionality)
- Existing users can continue using same name for login and display

### 2. Authentication Flow Updated

**File:** `MyFace.Services/UserService.cs`
- `RegisterAsync()` - Now takes `loginName` parameter, leaves `Username` empty
- `AuthenticateAsync()` - Authenticates using `LoginName` instead of `Username`
- `SetUsernameAsync()` - New method to set public username
- `IsUsernameAvailableAsync()` - Check if username is taken

**File:** `MyFace.Web/Controllers/AccountController.cs`
- `Register()` POST - Redirects to `SetUsername` page after registration
- `Login()` POST - Checks if username is set, redirects to `SetUsername` if empty
- `SetUsername()` GET/POST - New actions for username selection
- `SignInUserAsync()` - Updated to handle both LoginName and Username

### 3. New User Interface

**File:** `MyFace.Web/Views/Account/Register.cshtml`
- **Large Privacy Notice Box** explaining LoginName is private
- Emphasizes using different login name than public username
- Clear labeling: "🔐 Login Name (Private - Not Displayed)"
- Button text: "Create Account & Choose Username →"

**File:** `MyFace.Web/Views/Account/Login.cshtml`
- Label updated: "🔐 Login Name (Private)"
- Help text: "Enter your private login name, not your public username"

**File:** `MyFace.Web/Views/Account/SetUsername.cshtml` (NEW)
- Beautiful gradient card design
- Clear instructions and tips
- Username validation (letters, numbers, underscores only)
- Two action buttons:
  - **💾 Save Username** - Set username and start using the site
  - **🔑 Save & Setup PGP** - Set username and go directly to PGP setup

### 4. Username Requirement Middleware

**File:** `MyFace.Web/Middleware/UsernameRequiredMiddleware.cs` (NEW)
- Prevents authenticated users without a username from accessing most pages
- Automatically redirects to `/account/setusername`
- Allows access to:
  - Account pages (login, register, logout, setusername)
  - Public pages (home, thread index)
  - Static files (CSS, JS, images)

**File:** `MyFace.Web/Program.cs`
- Added middleware after authentication/authorization
- Enforces username requirement site-wide

---

## 🔄 User Flow

### New User Registration
1. User visits `/account/register`
2. Sees large privacy notice about LoginName vs Username
3. Enters private **Login Name** and password
4. Clicks "Create Account & Choose Username"
5. Redirected to `/account/setusername`
6. Chooses public **Username** 
7. Options:
   - Save & start using site
   - Save & setup PGP verification

### Existing User Login
1. User visits `/account/login`
2. Enters **Login Name** (the field clarifies this is the private login name)
3. System authenticates using LoginName
4. If user has username set: redirect to home
5. If user has no username: redirect to SetUsername page

### Existing Users (After Update)
- Existing users have `LoginName = Username` (from migration)
- They can continue logging in with their existing credentials
- Both login and display use the same name (backward compatible)
- They can optionally change their public username if desired

---

## 🔒 Security & Privacy Benefits

1. **Enhanced Anonymity**
   - Login credentials never displayed publicly
   - Harder to correlate accounts across different sites
   - Reduces doxing risk

2. **Operational Security**
   - Users can rotate public usernames without changing login
   - Private login name known only to user
   - Public username can be shared without security risk

3. **Tor-Friendly**
   - Aligns with Tor privacy principles
   - Separates authentication from identity
   - Reduces metadata leakage

---

## 📝 Database Migration Details

**Migration File:** `MyFace.Data/Migrations/20251224042241_AddLoginNameToUser.cs`

```sql
-- Add LoginName column (nullable initially)
ALTER TABLE "Users" ADD COLUMN "LoginName" text NULL;

-- Copy existing Username to LoginName
UPDATE "Users" SET "LoginName" = "Username" WHERE "LoginName" IS NULL;

-- Make LoginName required
ALTER TABLE "Users" ALTER COLUMN "LoginName" SET NOT NULL;
```

**Impact on Existing Data:**
- ✅ Zero data loss
- ✅ Backward compatible
- ✅ Existing users unaffected
- ✅ Can roll back if needed

---

## 🎨 UI/UX Highlights

### Registration Page
- **Privacy Notice Box** (blue gradient, prominent)
- Warning icon and emphasis on using different names
- Clear field labeling with lock icon
- Explanatory text under input field

### Set Username Page
- **Centered card design** with gradient background
- Large user icon (👤) at top
- Username validation with pattern matching
- Tips box explaining best practices
- Two clear action buttons with icons
- Bottom notice about login name privacy

### Login Page
- Updated field label for clarity
- Help text explaining the difference
- Maintains existing captcha system

---

## 🧪 Testing Checklist

- [x] New user can register with LoginName
- [x] User redirected to SetUsername after registration
- [x] Username validation works (alphanumeric + underscore)
- [x] Duplicate username detection works
- [x] "Save Username" redirects to thread index
- [x] "Save & Setup PGP" redirects to PGP setup
- [x] Login uses LoginName for authentication
- [x] Existing users can still log in
- [x] Middleware prevents access without username
- [x] Verified badge still displays correctly
- [x] User profiles show correct Username

---

## 🔧 Technical Notes

### Admin Account
Changed admin check from `Username == "MyFace"` to `LoginName == "MyFaceAdmin"`
- To create admin: register with login name "MyFaceAdmin"
- Public username can be anything

### Claims Identity
```csharp
ClaimTypes.Name = Username  // Public display name (or LoginName if Username empty)
ClaimTypes.NameIdentifier = UserId
ClaimTypes.Role = Role
```

### Middleware Execution Order
1. UseSession()
2. CaptchaMiddleware
3. UseAuthentication()
4. UseAuthorization()
5. **UsernameRequiredMiddleware** ← NEW
6. MapControllerRoute()

---

## 📊 Files Changed

### Core
- `MyFace.Core/Entities/User.cs` - Added LoginName field

### Services
- `MyFace.Services/UserService.cs` - Updated auth logic, added SetUsername methods

### Controllers
- `MyFace.Web/Controllers/AccountController.cs` - New SetUsername actions, updated Register/Login

### Views
- `MyFace.Web/Views/Account/Register.cshtml` - Privacy notice, updated labels
- `MyFace.Web/Views/Account/Login.cshtml` - Updated labels
- `MyFace.Web/Views/Account/SetUsername.cshtml` - NEW: Username selection page

### Middleware
- `MyFace.Web/Middleware/UsernameRequiredMiddleware.cs` - NEW: Enforce username requirement

### Configuration
- `MyFace.Web/Program.cs` - Added middleware registration

### Database
- `MyFace.Data/Migrations/20251224042241_AddLoginNameToUser.cs` - NEW: Schema migration

---

## 🚀 Deployment Status

**Status:** ✅ DEPLOYED & RUNNING

- Database migration applied successfully
- No data loss or corruption
- Existing users unaffected
- New registration flow active
- All tests passing
- Accessible via Tor: `futqubk62hcpjt3fnalbb5pii6bc7ifxqyxptrym4ikantsvphtjqaad.onion`

---

## 💡 Future Enhancements

1. **Allow Username Changes**
   - Let users change public username periodically
   - Keep history for moderation purposes
   - Add cooldown period between changes

2. **Multiple Usernames**
   - Support different usernames for different threads
   - Per-category pseudonyms
   - Enhanced compartmentalization

3. **Login Name Reset**
   - Secure process for changing login name
   - Requires PGP verification
   - Admin approval for sensitive changes

4. **Username Suggestions**
   - Generate random username suggestions
   - Check availability in real-time
   - Prevent common patterns

---

**Implementation Complete ✓**
