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

    public async Task UpdateProfileAsync(int userId, string aboutMe, string fontColor, string fontFamily)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.AboutMe = aboutMe ?? string.Empty;
            user.FontColor = fontColor ?? "#e5e7eb";
            user.FontFamily = fontFamily ?? "system-ui, -apple-system, sans-serif";
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateFullProfileAsync(int userId, string aboutMe, string fontColor, string fontFamily,
        string backgroundColor, string accentColor, string borderColor, string profileLayout,
        string buttonBackgroundColor, string buttonTextColor, string buttonBorderColor,
        string vendorShopDescription, string vendorPolicies, string vendorPayments, string vendorExternalReferences)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.AboutMe = aboutMe ?? string.Empty;
            user.FontColor = fontColor ?? "#e5e7eb";
            user.FontFamily = fontFamily ?? "system-ui, -apple-system, sans-serif";
            const int defaultFontSize = 14;
            user.FontSize = defaultFontSize;
            user.BackgroundColor = backgroundColor ?? "#0f172a";
            user.AccentColor = accentColor ?? "#3b82f6";
            user.BorderColor = borderColor ?? "#334155";
            user.ProfileLayout = profileLayout ?? "default";
            user.ButtonBackgroundColor = string.IsNullOrWhiteSpace(buttonBackgroundColor) ? "#0ea5e9" : buttonBackgroundColor;
            user.ButtonTextColor = string.IsNullOrWhiteSpace(buttonTextColor) ? "#ffffff" : buttonTextColor;
            user.ButtonBorderColor = string.IsNullOrWhiteSpace(buttonBorderColor) ? "#0ea5e9" : buttonBorderColor;
            user.VendorShopDescription = vendorShopDescription ?? string.Empty;
            user.VendorPolicies = vendorPolicies ?? string.Empty;
            user.VendorPayments = vendorPayments ?? string.Empty;
            user.VendorExternalReferences = vendorExternalReferences ?? string.Empty;
            await _context.SaveChangesAsync();
        }
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

    public async Task AddContactAsync(int userId, string service, string accountId)
    {
        var contact = new UserContact
        {
            UserId = userId,
            ServiceName = service,
            AccountId = accountId
        };
        _context.UserContacts.Add(contact);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveContactAsync(int userId, int contactId)
    {
        var contact = await _context.UserContacts.FindAsync(contactId);
        if (contact != null && contact.UserId == userId)
        {
            _context.UserContacts.Remove(contact);
            await _context.SaveChangesAsync();
        }
    }

    public async Task AddNewsAsync(int userId, string title, string content)
    {
        var news = new UserNews
        {
            UserId = userId,
            Title = title,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };
        _context.UserNews.Add(news);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveNewsAsync(int userId, int newsId)
    {
        var news = await _context.UserNews.FindAsync(newsId);
        if (news != null && news.UserId == userId)
        {
            _context.UserNews.Remove(news);
            await _context.SaveChangesAsync();
        }
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
