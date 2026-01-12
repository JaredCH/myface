# Input Validation Framework Plan

## ✅ INSPECTION COMPLETE - PARTIALLY IMPLEMENTED

**Date Completed:** January 5, 2026  
**Status:** ⚠️ Medium Risk - Validation exists but inconsistent coverage  
**Reason:** ASP.NET Core Data Annotations used, but not all inputs fully validated

---

## Inspection Findings

### ✅ Currently Implemented
- **Data Annotations on ViewModels** - RegisterViewModel, LoginViewModel, etc. use `[Required]`, `[StringLength]`, `[RegularExpression]`
- **ModelState validation** - Controllers check `ModelState.IsValid` before processing
- **XSS protection** - BBCodeFormatter HTML-encodes all user content
- **Length limits enforced** - RegisterViewModel: Username (3-50), Password (6-100)
- **Regex validation** - Username pattern: `^[a-zA-Z0-9_-]+$`
- **File upload scanning** - MalwareScanner configured for uploads
- **Manual validation** - Some controllers validate specific business rules

### ⚠️ Areas for Improvement
- **Inconsistent validation** - Not all form inputs have Data Annotations
- **Missing length limits** - Some text fields lack `[StringLength]` or `[MaxLength]`
- **No centralized validation** - Validation rules scattered across ViewModels
- **Thread/Post content limits** - Length limits exist but could be more explicit
- **URL parameter validation** - Route parameters validated minimally
- **File upload types** - File type validation beyond malware scanning needed

### 📊 Risk Assessment
**Current Risk Level:** 🟡 MEDIUM
- SQL injection risk: 🟢 LOW (Entity Framework parameterizes)
- XSS risk: 🟢 LOW (HTML encoding enforced)
- DoS via large input: 🟡 MEDIUM (some limits missing)
- Invalid data entry: 🟡 MEDIUM (inconsistent validation)

---

## Recommended Correction Steps

### Priority: Medium - Prevents data integrity and DoS issues

1. **Add validation to all ViewModels**
   - ThreadReplyViewModel - Add `[StringLength]` to Content field
   - CreateThreadViewModel - Enforce title and body length limits
   - AddMonitorViewModel - Validate URL format and length
   - ProfileViewModels - Validate AboutMe, UserNews, font, color fields

2. **Create validation attribute library**
   - `[SafeHtml]` - Validates no dangerous HTML tags
   - `[NoSqlInjection]` - Extra layer for raw SQL scenarios
   - `[FileExtension]` - Whitelist allowed file extensions
   - `[ColorHex]` - Validates hex color format (#RRGGBB)
   - `[FontFamily]` - Whitelist allowed font families

3. **Implement server-side length limits** (Priority: High)
   - Thread title: 200 characters max
   - Thread body: 10,000 characters max
   - Post content: 10,000 characters max
   - AboutMe: 5,000 characters max
   - UserNews: 2,000 characters max
   - Chat messages: Already limited (see ChatService)

4. **Add validation to Controllers**
   - ThreadController - Validate thread ID exists before operations
   - PostController - Check post ownership before edit
   - UserController - Validate username format on profile updates
   - FileController - Validate file size and extension whitelist

5. **Configure global validation settings**
   - Add `[MaxLength]` to Entity classes (database-level enforcement)
   - Configure request body size limits (already done: FormOptions)
   - Set up automatic validation error responses

6. **Implement rate limiting validation**
   - Check rate limits before processing expensive operations
   - Validate captcha completion where required
   - Already partially implemented (RateLimitService, CaptchaService)

### Example Implementations

Custom validation attribute:
```csharp
public class SafeHtmlAttribute : ValidationAttribute
{
    protected override ValidationResult IsValid(object value, ValidationContext context)
    {
        var input = value?.ToString();
        if (string.IsNullOrEmpty(input)) return ValidationResult.Success;
        
        // Check for dangerous patterns
        var dangerous = new[] { "<script", "javascript:", "onerror=", "onload=" };
        if (dangerous.Any(d => input.Contains(d, StringComparison.OrdinalIgnoreCase)))
        {
            return new ValidationResult("Content contains disallowed HTML.");
        }
        
        return ValidationResult.Success;
    }
}
```

Apply to ViewModels:
```csharp
public class ThreadReplyViewModel
{
    [Required]
    [StringLength(10000)]
    [SafeHtml]
    public string Content { get; set; }
}
```

---

## Conclusion

**Action Required:** Recommended - Improves data integrity and prevents abuse.

**Rationale:**
- Current validation prevents most security issues ✅
- Inconsistent validation creates gaps in defense
- Length limits prevent DoS attacks
- Explicit validation improves error messages

**Priority:** Medium - Implement before handling high-volume traffic

**Estimated Effort:** 8-12 hours (ViewModels + custom attributes + testing)

**Files to Update:**
- Models/ThreadReplyViewModel.cs
- Models/CreateThreadViewModel.cs
- Models/ProfileViewModels.cs
- Models/AddMonitorViewModel.cs
- Create: Validation/SafeHtmlAttribute.cs
- Create: Validation/ColorHexAttribute.cs
- Create: Validation/FileExtensionAttribute.cs
