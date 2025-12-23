using Microsoft.EntityFrameworkCore;
using MyFace.Core.Entities;
using ThreadEntity = MyFace.Core.Entities.Thread;
using MyFace.Data;

namespace MyFace.Services;

public class ForumService
{
    private readonly ApplicationDbContext _context;

    public ForumService(ApplicationDbContext context)
    {
        _context = context;
    }

    // Thread operations
    public async Task<ThreadEntity> CreateThreadAsync(string title, int? userId, bool isAnonymous)
    {
        var thread = new ThreadEntity
        {
            Title = title,
            UserId = userId,
            IsAnonymous = isAnonymous,
            CreatedAt = DateTime.UtcNow,
            IsLocked = false,
            IsPinned = false
        };

        _context.Threads.Add(thread);
        await _context.SaveChangesAsync();
        return thread;
    }

    public async Task<List<ThreadEntity>> GetThreadsAsync(int skip = 0, int take = 50)
    {
        return await _context.Threads
            .Include(t => t.User)
            .Include(t => t.Posts)
            .OrderByDescending(t => t.IsPinned)
            .ThenByDescending(t => t.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<ThreadEntity?> GetThreadByIdAsync(int id)
    {
        return await _context.Threads
            .Include(t => t.User)
            .Include(t => t.Posts)
                .ThenInclude(p => p.User)
            .Include(t => t.Posts)
                .ThenInclude(p => p.Votes)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Post?> GetPostByIdAsync(int postId)
    {
        return await _context.Posts.FindAsync(postId);
    }

    // Post operations
    public async Task<Post> CreatePostAsync(int threadId, string content, int? userId, bool isAnonymous)
    {
        var post = new Post
        {
            ThreadId = threadId,
            Content = content,
            UserId = userId,
            IsAnonymous = isAnonymous,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _context.Posts.Add(post);
        await _context.SaveChangesAsync();
        return post;
    }

    public async Task<bool> UpdatePostAsync(int postId, int userId, string content)
    {
        var post = await _context.Posts.FindAsync(postId);
        if (post == null || post.UserId != userId || post.IsDeleted)
        {
            return false;
        }

        post.Content = content;
        post.EditedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeletePostAsync(int postId, int userId)
    {
        var post = await _context.Posts.FindAsync(postId);
        if (post == null || post.UserId != userId)
        {
            return false;
        }

        post.IsDeleted = true;
        post.Content = "[deleted]";
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Post>> GetUserPostsAsync(int userId, int skip = 0, int take = 50)
    {
        return await _context.Posts
            .Include(p => p.Thread)
            .Where(p => p.UserId == userId && !p.IsDeleted && !p.IsAnonymous)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    // Vote operations
    public async Task<bool> VoteAsync(int postId, int? userId, string? sessionId, int value)
    {
        Vote? existingVote = null;
        if (userId != null)
        {
            existingVote = await _context.Votes
                .FirstOrDefaultAsync(v => v.PostId == postId && v.UserId == userId);
        }
        else if (!string.IsNullOrEmpty(sessionId))
        {
            existingVote = await _context.Votes
                .FirstOrDefaultAsync(v => v.PostId == postId && v.SessionId == sessionId);
        }

        if (existingVote != null)
        {
            if (existingVote.Value == value)
            {
                // Remove vote if clicking the same button
                _context.Votes.Remove(existingVote);
            }
            else
            {
                // Change vote
                existingVote.Value = value;
            }
        }
        else
        {
            // New vote
            var vote = new Vote
            {
                PostId = postId,
                UserId = userId,
                SessionId = userId == null ? sessionId : null,
                Value = value,
                CreatedAt = DateTime.UtcNow
            };
            _context.Votes.Add(vote);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetPostScoreAsync(int postId)
    {
        var votes = await _context.Votes
            .Where(v => v.PostId == postId)
            .ToListAsync();

        return votes.Sum(v => v.Value);
    }
}
