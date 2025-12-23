using System.Text;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using MyFace.Core.Entities;
using MyFace.Data;

namespace MyFace.Services;

public class RssService
{
    private readonly ApplicationDbContext _context;

    public RssService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateThreadsFeedAsync(string baseUrl)
    {
        var threads = await _context.Threads
            .Include(t => t.User)
            .OrderByDescending(t => t.CreatedAt)
            .Take(50)
            .ToListAsync();

        var sb = new StringBuilder();
        using (var writer = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, Async = true }))
        {
            await writer.WriteStartDocumentAsync();
            await writer.WriteStartElementAsync(null, "rss", null);
            await writer.WriteAttributeStringAsync(null, "version", null, "2.0");

            await writer.WriteStartElementAsync(null, "channel", null);
            await writer.WriteElementStringAsync(null, "title", null, "MyFace - Latest Threads");
            await writer.WriteElementStringAsync(null, "link", null, baseUrl);
            await writer.WriteElementStringAsync(null, "description", null, "Latest discussion threads on MyFace");

            foreach (var thread in threads)
            {
                await writer.WriteStartElementAsync(null, "item", null);
                await writer.WriteElementStringAsync(null, "title", null, thread.Title);
                await writer.WriteElementStringAsync(null, "link", null, $"{baseUrl}/Thread/View/{thread.Id}");
                await writer.WriteElementStringAsync(null, "guid", null, $"{baseUrl}/Thread/View/{thread.Id}");
                await writer.WriteElementStringAsync(null, "pubDate", null, thread.CreatedAt.ToString("R"));
                
                var author = thread.IsAnonymous ? "Anonymous" : thread.User?.Username ?? "Anonymous";
                await writer.WriteElementStringAsync(null, "author", null, author);
                
                await writer.WriteEndElementAsync(); // item
            }

            await writer.WriteEndElementAsync(); // channel
            await writer.WriteEndElementAsync(); // rss
            await writer.WriteEndDocumentAsync();
        }

        return sb.ToString();
    }

    public async Task<string> GenerateUserFeedAsync(string username, string baseUrl)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == username);

        if (user == null)
        {
            return string.Empty;
        }

        var posts = await _context.Posts
            .Include(p => p.Thread)
            .Where(p => p.UserId == user.Id && !p.IsDeleted && !p.IsAnonymous)
            .OrderByDescending(p => p.CreatedAt)
            .Take(50)
            .ToListAsync();

        var sb = new StringBuilder();
        using (var writer = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, Async = true }))
        {
            await writer.WriteStartDocumentAsync();
            await writer.WriteStartElementAsync(null, "rss", null);
            await writer.WriteAttributeStringAsync(null, "version", null, "2.0");

            await writer.WriteStartElementAsync(null, "channel", null);
            await writer.WriteElementStringAsync(null, "title", null, $"MyFace - {username}'s Posts");
            await writer.WriteElementStringAsync(null, "link", null, $"{baseUrl}/User/{username}");
            await writer.WriteElementStringAsync(null, "description", null, $"Latest posts by {username}");

            foreach (var post in posts)
            {
                await writer.WriteStartElementAsync(null, "item", null);
                await writer.WriteElementStringAsync(null, "title", null, $"Post in: {post.Thread.Title}");
                await writer.WriteElementStringAsync(null, "link", null, $"{baseUrl}/Thread/View/{post.ThreadId}#post-{post.Id}");
                await writer.WriteElementStringAsync(null, "guid", null, $"{baseUrl}/Thread/View/{post.ThreadId}#post-{post.Id}");
                await writer.WriteElementStringAsync(null, "pubDate", null, post.CreatedAt.ToString("R"));
                await writer.WriteElementStringAsync(null, "description", null, post.Content);
                
                await writer.WriteEndElementAsync(); // item
            }

            await writer.WriteEndElementAsync(); // channel
            await writer.WriteEndElementAsync(); // rss
            await writer.WriteEndDocumentAsync();
        }

        return sb.ToString();
    }

    public async Task<string> GenerateMonitorFeedAsync(string baseUrl)
    {
        var monitors = await _context.OnionMonitors
            .OrderByDescending(m => m.LastChecked)
            .Take(100)
            .ToListAsync();

        var sb = new StringBuilder();
        using (var writer = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, Async = true }))
        {
            await writer.WriteStartDocumentAsync();
            await writer.WriteStartElementAsync(null, "rss", null);
            await writer.WriteAttributeStringAsync(null, "version", null, "2.0");

            await writer.WriteStartElementAsync(null, "channel", null);
            await writer.WriteElementStringAsync(null, "title", null, "MyFace - Onion Monitor Status");
            await writer.WriteElementStringAsync(null, "link", null, $"{baseUrl}/Monitor");
            await writer.WriteElementStringAsync(null, "description", null, "Status updates for monitored .onion sites");

            foreach (var monitor in monitors)
            {
                await writer.WriteStartElementAsync(null, "item", null);
                var title = $"{monitor.FriendlyName ?? monitor.OnionUrl} - {(monitor.IsOnline ? "Online" : "Offline")}";
                await writer.WriteElementStringAsync(null, "title", null, title);
                await writer.WriteElementStringAsync(null, "link", null, monitor.OnionUrl);
                await writer.WriteElementStringAsync(null, "guid", null, $"{baseUrl}/Monitor#{monitor.Id}");
                
                var pubDate = monitor.LastChecked ?? monitor.CreatedAt;
                await writer.WriteElementStringAsync(null, "pubDate", null, pubDate.ToString("R"));
                
                var description = $"Status: {(monitor.IsOnline ? "Online" : "Offline")}\n" +
                                $"Last Checked: {monitor.LastChecked?.ToString("u")}\n" +
                                $"Last Online: {monitor.LastOnline?.ToString("u")}";
                await writer.WriteElementStringAsync(null, "description", null, description);
                
                await writer.WriteEndElementAsync(); // item
            }

            await writer.WriteEndElementAsync(); // channel
            await writer.WriteEndElementAsync(); // rss
            await writer.WriteEndDocumentAsync();
        }

        return sb.ToString();
    }
}
