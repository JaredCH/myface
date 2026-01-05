# Parameterized Query Implementation Plan

## ✅ INSPECTION COMPLETE - MOSTLY SECURE

**Date Completed:** January 5, 2026  
**Status:** ⚠️ Low Risk - Entity Framework used with 2 raw SQL instances  
**Reason:** EF Core handles parameterization, but limited raw SQL found

---

## Inspection Findings

### ✅ Strengths
- **Entity Framework Core used exclusively** - All data access through EF Core DbContext
- **No string concatenation** - No SQL queries built with string concatenation found
- **LINQ queries** - All business logic uses LINQ to Entities (automatically parameterized)
- **No FromSqlRaw with user input** - Raw SQL only used for schema creation
- **Existing security audit confirms** - SECURITY_AUDIT.md states "No SQL injection vulnerabilities"

### ⚠️ Raw SQL Usage (Schema Creation Only)
**Found 2 instances of ExecuteSqlRawAsync:**

1. **MailService.cs (line 53)** - Database schema creation
   - **Usage:** `ExecuteSqlRawAsync(sql, ct)` with hardcoded DDL
   - **User input:** None - SQL is constant string literal
   - **Risk:** 🟢 NONE - No user input, schema creation only
   - **Purpose:** Creates PrivateMessages table if not exists

2. **ChatService.cs (line 183)** - Database schema creation  
   - **Usage:** `ExecuteSqlRawAsync(sql, ct)` with hardcoded DDL
   - **User input:** None - SQL is constant string literal
   - **Risk:** 🟢 NONE - No user input, schema creation only
   - **Purpose:** Creates ChatMessages table if not exists

### 🔍 Code Analysis
```csharp
// MailService.cs - Safe (no user input)
var sql = """
CREATE TABLE IF NOT EXISTS "PrivateMessages" (
    "Id" SERIAL PRIMARY KEY,
    ...
);
""";
await _db.Database.ExecuteSqlRawAsync(sql, ct);

// ChatService.cs - Safe (no user input)
var sql = """
CREATE TABLE IF NOT EXISTS "ChatMessages" (
    ...
);
""";
await _db.Database.ExecuteSqlRawAsync(sql, ct);
```

### 📊 Risk Assessment
**Current Risk Level:** 🟢 LOW
- SQL injection risk: 🟢 NONE (EF Core + no user input in raw SQL)
- Data access: 100% through EF Core ✅
- Query parameterization: Automatic via LINQ ✅
- Raw SQL security: ✅ SECURE (no user input)

---

## Recommended Correction Steps (Optional Enhancement)

### Priority: Low - Current implementation is secure

1. **Migrate schema creation to EF Migrations** (Optional)
   - Replace raw SQL schema creation with proper EF migrations
   - Use `dotnet ef migrations add CreateMailTables`
   - Provides version control and better schema management
   - Benefit: More maintainable, but not a security improvement

2. **Add static analysis to CI/CD** (Recommended)
   - Configure code analysis rules to flag raw SQL usage
   - Set up alerts for `ExecuteSqlRaw` or `FromSqlRaw` usage
   - Requires manual review when new raw SQL is added
   - Benefit: Prevents future SQL injection vulnerabilities

3. **Document safe raw SQL usage policy**
   - Create coding standards for when raw SQL is acceptable
   - Require code review for any raw SQL additions
   - Specify: "Raw SQL only for schema DDL with no user input"
   - Benefit: Prevents regression

### Example CI/CD Rule (Optional)

Add to .editorconfig or code analysis config:
```ini
# Warn on raw SQL usage - requires manual review
dotnet_diagnostic.CA2100.severity = warning  # Review SQL queries for security
```

### Example Migration (Optional Alternative)

Instead of raw SQL in MailService:
```csharp
// Create migration class
public class CreatePrivateMessagesTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PrivateMessages",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", 
                        NpgsqlValueGenerationStrategy.SerialColumn),
                SenderId = table.Column<int>(nullable: true),
                // ... etc
            });
    }
}
```

---

## Conclusion

**Action Required:** ✅ None - Current implementation is secure

**Rationale:**
- Entity Framework Core handles parameterization automatically
- Raw SQL usage is limited to schema creation (no user input)
- No SQL injection vulnerabilities exist
- LINQ queries are safe by design

**Security Grade:** A

**Optional Enhancements:** 
- Migrate to EF Migrations for better maintainability
- Add static analysis to prevent future raw SQL with user input
- Document safe raw SQL policy

**Priority:** Low - Current approach is secure, migrations are a code quality improvement

**Estimated Effort:** 0 hours required, or 4-6 hours for migration refactoring (optional)
