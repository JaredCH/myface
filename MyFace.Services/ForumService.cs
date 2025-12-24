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

    // Helper to track activity (just adds to context, doesn't save)
    private void TrackActivity(string activityType, int? userId)
    {
        var activity = new Activity
        {
            ActivityType = activityType,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };
        _context.Activities.Add(activity);
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
        TrackActivity("thread", userId);
        await _context.SaveChangesAsync();
        return thread;
    }

    public async Task<List<ThreadEntity>> GetThreadsAsync(int skip = 0, int take = 50)
    {
        return await _context.Threads
            .Include(t => t.User)
                .ThenInclude(u => u.PGPVerifications)
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
                .ThenInclude(u => u.PGPVerifications)
            .Include(t => t.Posts)
                .ThenInclude(p => p.User)
                    .ThenInclude(u => u.PGPVerifications)
            .Include(t => t.Posts)
                .ThenInclude(p => p.EditedByUser)
                    .ThenInclude(u => u.PGPVerifications)
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
        TrackActivity("comment", userId);
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
        post.EditedByUserId = userId;
        post.EditCount++;
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

    // Admin/Mod post management
    public async Task<bool> AdminDeletePostAsync(int postId)
    {
        var post = await _context.Posts.FindAsync(postId);
        if (post == null)
        {
            return false;
        }

        post.IsDeleted = true;
        post.Content = "[removed by moderator]";
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AdminEditPostAsync(int postId, string content)
    {
        var post = await _context.Posts.FindAsync(postId);
        if (post == null)
        {
            return false;
        }

        post.Content = content;
        post.EditedAt = DateTime.UtcNow;
        post.EditCount++;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetStickyAsync(int postId, bool isSticky)
    {
        var post = await _context.Posts.FindAsync(postId);
        if (post == null)
        {
            return false;
        }

        post.IsSticky = isSticky;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> LockThreadAsync(int threadId, bool isLocked)
    {
        var thread = await _context.Threads.FindAsync(threadId);
        if (thread == null)
        {
            return false;
        }

        thread.IsLocked = isLocked;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetThreadCategoryAsync(int threadId, string category)
    {
        var thread = await _context.Threads.FindAsync(threadId);
        if (thread == null)
        {
            return false;
        }

        thread.Category = category;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteThreadAsync(int threadId)
    {
        var thread = await _context.Threads.FindAsync(threadId);
        if (thread == null)
        {
            return false;
        }

        _context.Threads.Remove(thread);
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

        // Track activity
        if (value > 0)
            TrackActivity("upvote", userId);
        else
            TrackActivity("downvote", userId);
        
        await _context.SaveChangesAsync();
        await UpdateUserVoteStatisticsAsync();
        return true;
    }

    public async Task<int> GetPostScoreAsync(int postId)
    {
        var votes = await _context.Votes
            .Where(v => v.PostId == postId)
            .ToListAsync();

        return votes.Sum(v => v.Value);
    }

    // Thread voting
    public async Task<bool> VoteOnThreadAsync(int threadId, int? userId, string? sessionId, int value)
    {
        Vote? existingVote = null;
        if (userId != null)
        {
            existingVote = await _context.Votes
                .FirstOrDefaultAsync(v => v.ThreadId == threadId && v.UserId == userId);
        }
        else if (!string.IsNullOrEmpty(sessionId))
        {
            existingVote = await _context.Votes
                .FirstOrDefaultAsync(v => v.ThreadId == threadId && v.SessionId == sessionId);
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
                ThreadId = threadId,
                UserId = userId,
                SessionId = userId == null ? sessionId : null,
                Value = value,
                CreatedAt = DateTime.UtcNow
            };
            _context.Votes.Add(vote);
        }

        // Track activity
        if (value > 0)
            TrackActivity("thread_upvote", userId);
        else
            TrackActivity("thread_downvote", userId);
        
        await _context.SaveChangesAsync();
        await UpdateUserVoteStatisticsAsync();
        return true;
    }

    public async Task<int> GetThreadScoreAsync(int threadId)
    {
        var votes = await _context.Votes
            .Where(v => v.ThreadId == threadId)
            .ToListAsync();

        return votes.Sum(v => v.Value);
    }

    // Update user vote statistics (simplified - don't load all data)
    public async Task UpdateUserVoteStatisticsAsync()
    {
        // Temporarily disabled for performance - will be calculated on demand
        // This was causing major performance issues by loading all users/posts/threads
        await Task.CompletedTask;
    }

    // Wilson Score + Time Decay for Hot ranking
    public double CalculateHotScore(int upvotes, int downvotes, DateTime createdAt)
    {
        int total = upvotes + downvotes;
        if (total == 0) return 0;

        // Wilson score confidence interval
        double z = 1.96; // 95% confidence
        double phat = (double)upvotes / total;
        double wilson = (phat + z * z / (2 * total) - z * Math.Sqrt((phat * (1 - phat) + z * z / (4 * total)) / total)) / (1 + z * z / total);

        // Time decay
        double daysOld = (DateTime.UtcNow - createdAt).TotalDays;
        double timeDecay = 1.0 / (daysOld + 0.5);

        return wilson * timeDecay * 1000; // Scale up for easier sorting
    }

    public async Task<List<ThreadEntity>> GetHotThreadsAsync(int take = 25)
    {
        var threads = await _context.Threads
            .Include(t => t.User)
                .ThenInclude(u => u.PGPVerifications)
            .Include(t => t.Posts)
                .ThenInclude(p => p.Votes)
            .Include(t => t.Votes) // Include thread votes
            .Where(t => !t.IsLocked)
            .ToListAsync();

        // Calculate hot scores
        var rankedThreads = threads
            .Select(t => new
            {
                Thread = t,
                HotScore = CalculateHotScoreForThread(t)
            })
            .OrderByDescending(x => x.HotScore)
            .Take(take)
            .Select(x => x.Thread)
            .ToList();

        // Failsafe: if no hot threads, return by thread vote count
        if (!rankedThreads.Any() || rankedThreads.All(t => CalculateHotScoreForThread(t) == 0))
        {
            return threads
                .OrderByDescending(t => t.Votes.Sum(v => v.Value))
                .ThenByDescending(t => t.Posts.SelectMany(p => p.Votes).Sum(v => v.Value))
                .Take(take)
                .ToList();
        }

        return rankedThreads;
    }

    private double CalculateHotScoreForThread(ThreadEntity thread)
    {
        // Thread votes weight: 70%, post votes weight: 30%
        var threadVotes = thread.Votes.ToList();
        int threadUpvotes = threadVotes.Count(v => v.Value > 0);
        int threadDownvotes = threadVotes.Count(v => v.Value < 0);
        
        var postVotes = thread.Posts.SelectMany(p => p.Votes).ToList();
        int postUpvotes = postVotes.Count(v => v.Value > 0);
        int postDownvotes = postVotes.Count(v => v.Value < 0);
        
        // Combine with weighting
        int totalUpvotes = (int)(threadUpvotes * 0.7 + postUpvotes * 0.3);
        int totalDownvotes = (int)(threadDownvotes * 0.7 + postDownvotes * 0.3);
        
        return CalculateHotScore(totalUpvotes, totalDownvotes, thread.CreatedAt);
    }

    public async Task<List<Post>> GetHotPostsForThreadAsync(int threadId, int skip, int take)
    {
        var posts = await _context.Posts
            .Include(p => p.User)
                .ThenInclude(u => u.PGPVerifications)
            .Include(p => p.Votes)
            .Where(p => p.ThreadId == threadId && !p.IsDeleted)
            .ToListAsync();

        // Calculate hot scores
        var rankedPosts = posts
            .Select(p => new
            {
                Post = p,
                HotScore = CalculateHotScore(
                    p.Votes.Count(v => v.Value > 0),
                    p.Votes.Count(v => v.Value < 0),
                    p.CreatedAt
                )
            })
            .OrderByDescending(x => x.HotScore)
            .Skip(skip)
            .Take(take)
            .Select(x => x.Post)
            .ToList();

        // Failsafe: if no hot posts, return by vote count
        if (!rankedPosts.Any() || rankedPosts.All(p => p.Votes.Sum(v => v.Value) == 0))
        {
            return posts
                .OrderByDescending(p => p.Votes.Sum(v => v.Value))
                .Skip(skip)
                .Take(take)
                .ToList();
        }

        return rankedPosts;
    }
}

