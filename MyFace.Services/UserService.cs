using System.Text;
using Microsoft.EntityFrameworkCore;
using MyFace.Core.Entities;
using MyFace.Data;
using Isopoh.Cryptography.Argon2;

namespace MyFace.Services;

public class UserService
{
    private readonly ApplicationDbContext _context;

    public UserService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<User?> RegisterAsync(string loginName, string password, string? pgpPublicKey = null)
    {
        if (await _context.Users.AnyAsync(u => u.LoginName == loginName))
        {
            return null; // LoginName already exists
        }

        var role = "User";
        if (loginName.Equals("MyFaceAdmin", StringComparison.OrdinalIgnoreCase))
        {
            role = "Admin";
        }

        var user = new User
        {
            LoginName = loginName,
            Username = string.Empty, // Must be set by user after registration
            PasswordHash = HashPassword(password),
            PgpPublicKey = pgpPublicKey,
            Role = role,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Authenticate user with constant-time response to prevent account enumeration.
    /// NOTE: LoginName is stored in plaintext because hashing would prevent lookups.
    /// This is a security trade-off - if DB is compromised, login names are exposed.
    /// Passwords remain protected with Argon2 hashing.
    /// </summary>
    public async Task<User?> AuthenticateAsync(string loginName, string password)
    {
        // Always query to prevent timing attacks that reveal if user exists
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.LoginName == loginName && u.IsActive);

        // Always verify password hash (even if user is null) for constant-time response
        var isValid = false;
        if (user != null)
        {
            isValid = VerifyPassword(password, user.PasswordHash);
        }
        else
        {
            // Perform dummy hash verification to maintain constant time
            // This prevents timing attacks that could enumerate valid usernames
            VerifyPassword(password, "$argon2id$v=19$m=65536,t=3,p=1$fakesaltfakesalt$fakehashfakehashfakehashfakehash");
        }

        if (!isValid || user == null)
        {
            return null;
        }

        user.LastSeenAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return user;
    }

    public async Task SetRoleAsync(int userId, string role)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.Role = role;
            await _context.SaveChangesAsync();
        }
    }

    public async Task SuspendUserAsync(int userId, DateTime? until)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.SuspendedUntil = until;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> SetActiveStateAsync(int userId, bool isActive)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return false;
        }

        user.IsActive = isActive;
        if (isActive)
        {
            user.SuspendedUntil = null;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _context.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        return await GetByIdAsync(id);
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        return await _context.Users
            .Include(u => u.Contacts)
            .Include(u => u.News)
            .Include(u => u.PGPVerifications)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<UserContact?> GetContactByIdAsync(int contactId)
    {
        return await _context.UserContacts
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == contactId);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _context.Users
            .Include(u => u.Contacts)
            .Include(u => u.News)
            .Include(u => u.PGPVerifications)
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<UserNews?> GetNewsByIdAsync(int id)
    {
        return await _context.UserNews
            .Include(n => n.User)
            .FirstOrDefaultAsync(n => n.Id == id);
    }

    public async Task<bool> SetUsernameAsync(int userId, string username)
    {
        // Check if username is already taken
        if (await _context.Users.AnyAsync(u => u.Username == username && u.Id != userId))
        {
            return false;
        }

        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.Username = username;
            await _context.SaveChangesAsync();
            return true;
        }
        return false;
    }

    public async Task<bool> IsUsernameAvailableAsync(string username)
    {
        return !await _context.Users.AnyAsync(u => u.Username == username);
    }

    public async Task ClearPgpKeyAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.PgpPublicKey = string.Empty;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<UserReviewSummaryResult> GetUserReviewSummaryAsync(int targetUserId, int recentCount = 3)
    {
        var query = _context.UserReviews
            .AsNoTracking()
            .Where(r => r.TargetUserId == targetUserId);

        var total = await query.CountAsync();
        if (total == 0)
        {
            return new UserReviewSummaryResult(0, 0, 0, Array.Empty<UserReview>());
        }

        var average = await query.AverageAsync(r => r.OverallScore);
        var positive = await query.CountAsync(r => r.OverallScore >= 4);
        var recent = recentCount > 0
            ? await query
                .OrderByDescending(r => r.CreatedAt)
                .Take(recentCount)
                .Include(r => r.ReviewerUser)
                .ToListAsync()
            : new List<UserReview>();

        return new UserReviewSummaryResult(total, average, positive, recent);
    }

    public async Task<List<UserReview>> GetUserReviewsAsync(int targetUserId, int skip, int take)
    {
        return await _context.UserReviews
            .Where(r => r.TargetUserId == targetUserId)
            .OrderByDescending(r => r.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Include(r => r.ReviewerUser)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<UserReview?> GetExistingReviewAsync(int targetUserId, int reviewerUserId)
    {
        return await _context.UserReviews
            .FirstOrDefaultAsync(r => r.TargetUserId == targetUserId && r.ReviewerUserId == reviewerUserId);
    }

    public async Task<UserReview> UpsertReviewAsync(int targetUserId, int reviewerUserId,
        int communicationScore, int shippingScore, int qualityScore, int overallScore, string comment)
    {
        var normalizedComment = NormalizeReviewComment(comment);

        var review = await _context.UserReviews
            .FirstOrDefaultAsync(r => r.TargetUserId == targetUserId && r.ReviewerUserId == reviewerUserId);

        if (review == null)
        {
            review = new UserReview
            {
                TargetUserId = targetUserId,
                ReviewerUserId = reviewerUserId,
                CommunicationScore = communicationScore,
                ShippingScore = shippingScore,
                QualityScore = qualityScore,
                OverallScore = overallScore,
                Comment = normalizedComment,
                CreatedAt = DateTime.UtcNow
            };
            _context.UserReviews.Add(review);
        }
        else
        {
            review.CommunicationScore = communicationScore;
            review.ShippingScore = shippingScore;
            review.QualityScore = qualityScore;
            review.OverallScore = overallScore;
            review.Comment = normalizedComment;
            review.CreatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return review;
    }

    private static string NormalizeReviewComment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "No details provided.";
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 2000 ? trimmed : trimmed[..2000];
    }

    public async Task<List<User>> SearchUsersByUsernameAsync(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return await _context.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
        }

        return await _context.Users
            .Where(u => u.Username.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> DeleteUserAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return false;
        }

        // Remove all related data
        _context.UserNews.RemoveRange(_context.UserNews.Where(n => n.UserId == userId));
        _context.UserContacts.RemoveRange(_context.UserContacts.Where(c => c.UserId == userId));
        _context.PGPVerifications.RemoveRange(_context.PGPVerifications.Where(p => p.UserId == userId));
        
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ChangeUsernameByAdminAsync(int userId, string newUsername, int adminId, string? adminNote = null)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null || string.IsNullOrWhiteSpace(newUsername))
        {
            return false;
        }

        // Check if new username is already taken
        if (await _context.Users.AnyAsync(u => u.Username == newUsername && u.Id != userId))
        {
            return false;
        }

        var oldUsername = user.Username;
        
        // Reset username to empty so user must choose new one
        user.Username = string.Empty;
        user.MustChangeUsername = true;
        user.UsernameChangedByAdminId = adminId;

        // Log the change
        var changeLog = new UsernameChangeLog
        {
            UserId = userId,
            OldUsername = oldUsername,
            NewUsername = newUsername, // This is the suggested/reset username
            AdminNote = adminNote,
            ChangedByUserId = adminId,
            ChangedAt = DateTime.UtcNow,
            UserNotified = false
        };

        _context.UsernameChangeLogs.Add(changeLog);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return false;
        }

        if (!VerifyPassword(currentPassword, user.PasswordHash))
        {
            return false;
        }

        user.PasswordHash = HashPassword(newPassword);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AdminSetPasswordAsync(int userId, string newPassword)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return false;
        }

        user.PasswordHash = HashPassword(newPassword);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<UsernameChangeLog?> GetUnnotifiedUsernameChangeAsync(int userId)
    {
        return await _context.UsernameChangeLogs
            .Include(log => log.ChangedByUser)
            .Where(log => log.UserId == userId && !log.UserNotified)
            .OrderByDescending(log => log.ChangedAt)
            .FirstOrDefaultAsync();
    }

    public async Task MarkUsernameChangeNotifiedAsync(int logId)
    {
        var log = await _context.UsernameChangeLogs.FindAsync(logId);
        if (log != null)
        {
            log.UserNotified = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task ClearMustChangeUsernameAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.MustChangeUsername = false;
            await _context.SaveChangesAsync();
        }
    }

    private static string HashPassword(string password)
    {
        // PHC formatted Argon2id hash
        return Argon2.Hash(password);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        return Argon2.Verify(hash, password);
    }
}

    public record UserReviewSummaryResult(int TotalReviews, double AverageScore, int PositiveReviews, IReadOnlyList<UserReview> RecentReviews);
