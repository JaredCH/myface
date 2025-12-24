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

    public async Task<User?> RegisterAsync(string username, string password, string? pgpPublicKey = null)
    {
        if (await _context.Users.AnyAsync(u => u.Username == username))
        {
            return null; // Username already exists
        }

        var role = "User";
        if (username.Equals("MyFace", StringComparison.OrdinalIgnoreCase))
        {
            role = "Admin";
        }

        var user = new User
        {
            Username = username,
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

    public async Task<User?> AuthenticateAsync(string username, string password)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

        if (user == null || !VerifyPassword(password, user.PasswordHash))
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
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _context.Users
            .Include(u => u.Contacts)
            .Include(u => u.News)
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
            user.AboutMe = aboutMe;
            user.FontColor = fontColor;
            user.FontFamily = fontFamily;
            await _context.SaveChangesAsync();
        }
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
