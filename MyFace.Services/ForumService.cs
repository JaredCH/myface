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

    public async Task<bool> UpdatePostAsync(int postId, int userId, string content, bool allowOverride = false)
    {
        var post = await _context.Posts.FindAsync(postId);
        if (post == null || post.IsDeleted)
        {
            return false;
        }

        if (!allowOverride && post.UserId != userId)
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

    public async Task<bool> AdminEditPostAsync(int postId, string content, int? editedByUserId = null)
    {
        var post = await _context.Posts.FindAsync(postId);
        if (post == null)
        {
            return false;
        }

        post.Content = content;
        post.EditedAt = DateTime.UtcNow;
        post.EditedByUserId = editedByUserId;
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

    public async Task<List<UserActivityItem>> GetUserActivityAsync(int userId, string? search, DateTime? start, DateTime? end, string? sort)
    {
        var searchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLower();
        var startUtc = start?.ToUniversalTime();
        var endUtc = end?.ToUniversalTime();

        var postsQuery = _context.Posts
            .Include(p => p.Thread)
            .Where(p => p.UserId == userId && !p.IsDeleted && !p.IsAnonymous);

        if (startUtc.HasValue)
        {
            postsQuery = postsQuery.Where(p => p.CreatedAt >= startUtc.Value);
        }

        if (endUtc.HasValue)
        {
            postsQuery = postsQuery.Where(p => p.CreatedAt <= endUtc.Value);
        }

        if (searchTerm != null)
        {
            postsQuery = postsQuery.Where(p =>
                ((p.Content ?? string.Empty).ToLower().Contains(searchTerm)) ||
                (p.Thread != null && p.Thread.Title.ToLower().Contains(searchTerm)));
        }

        var postItems = await postsQuery
            .Select(p => new UserActivityItem(
                "Comment",
                p.Thread != null ? p.Thread.Title : "Thread",
                p.Content,
                p.CreatedAt,
                p.ThreadId,
                p.Id,
                null))
            .ToListAsync();

        var threadsQuery = _context.Threads
            .Where(t => t.UserId == userId);

        if (startUtc.HasValue)
        {
            threadsQuery = threadsQuery.Where(t => t.CreatedAt >= startUtc.Value);
        }

        if (endUtc.HasValue)
        {
            threadsQuery = threadsQuery.Where(t => t.CreatedAt <= endUtc.Value);
        }

        if (searchTerm != null)
        {
            threadsQuery = threadsQuery.Where(t => t.Title.ToLower().Contains(searchTerm));
        }

        var threadItems = await threadsQuery
            .Select(t => new UserActivityItem(
                "Thread",
                t.Title,
                string.Empty,
                t.CreatedAt,
                t.Id,
                null,
                null))
            .ToListAsync();

        var newsQuery = _context.UserNews
            .Where(n => n.UserId == userId);

        if (startUtc.HasValue)
        {
            newsQuery = newsQuery.Where(n => n.CreatedAt >= startUtc.Value);
        }

        if (endUtc.HasValue)
        {
            newsQuery = newsQuery.Where(n => n.CreatedAt <= endUtc.Value);
        }

        if (searchTerm != null)
        {
            newsQuery = newsQuery.Where(n =>
                n.Title.ToLower().Contains(searchTerm) ||
                n.Content.ToLower().Contains(searchTerm));
        }

        var newsItems = await newsQuery
            .Select(n => new UserActivityItem(
                "News",
                n.Title,
                n.Content,
                n.CreatedAt,
                null,
                null,
                n.Id))
            .ToListAsync();

        var items = postItems
            .Concat(threadItems)
            .Concat(newsItems)
            .ToList();

        var sortKey = string.IsNullOrWhiteSpace(sort) ? "newest" : sort.Trim().ToLower();
        return sortKey switch
        {
            "oldest" => items.OrderBy(i => i.CreatedAt).ToList(),
            "title" => items.OrderBy(i => i.Title).ToList(),
            _ => items.OrderByDescending(i => i.CreatedAt).ToList()
        };
    }

    // Vote operations
    public async Task<bool> VoteAsync(int postId, int? userId, string? sessionId, int value)
    {
        var targetUserId = await _context.Posts
            .Where(p => p.Id == postId)
            .Select(p => p.UserId)
            .FirstOrDefaultAsync();

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

        if (targetUserId.HasValue)
        {
            await UpdateUserVoteStatisticsAsync(targetUserId.Value);
        }
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
        var targetUserId = await _context.Threads
            .Where(t => t.Id == threadId)
            .Select(t => t.UserId)
            .FirstOrDefaultAsync();

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

        if (targetUserId.HasValue)
        {
            await UpdateUserVoteStatisticsAsync(targetUserId.Value);
        }
        return true;
    }

    public async Task<int> GetThreadScoreAsync(int threadId)
    {
        var votes = await _context.Votes
            .Where(v => v.ThreadId == threadId)
            .ToListAsync();

        return votes.Sum(v => v.Value);
    }

    public async Task<UserVoteStats> CalculateUserVoteStatsAsync(int userId)
    {
        var threadUpvotes = await _context.Votes
            .Where(v => v.ThreadId != null && v.Thread != null && v.Thread.UserId == userId)
            .CountAsync(v => v.Value > 0);

        var threadDownvotes = await _context.Votes
            .Where(v => v.ThreadId != null && v.Thread != null && v.Thread.UserId == userId)
            .CountAsync(v => v.Value < 0);

        var commentUpvotes = await _context.Votes
            .Where(v => v.PostId != null && v.Post != null && v.Post.UserId == userId)
            .CountAsync(v => v.Value > 0);

        var commentDownvotes = await _context.Votes
            .Where(v => v.PostId != null && v.Post != null && v.Post.UserId == userId)
            .CountAsync(v => v.Value < 0);

        return new UserVoteStats(threadUpvotes, threadDownvotes, commentUpvotes, commentDownvotes);
    }

    public async Task UpdateUserVoteStatisticsAsync(int userId)
    {
        var stats = await CalculateUserVoteStatsAsync(userId);
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return;
        }

        user.PostUpvotes = stats.ThreadUpvotes;
        user.PostDownvotes = stats.ThreadDownvotes;
        user.CommentUpvotes = stats.CommentUpvotes;
        user.CommentDownvotes = stats.CommentDownvotes;

        await _context.SaveChangesAsync();
    }

    public record UserActivityItem(string Type, string Title, string? Content, DateTime CreatedAt, int? ThreadId, int? PostId, int? NewsId);
    public record UserVoteStats(int ThreadUpvotes, int ThreadDownvotes, int CommentUpvotes, int CommentDownvotes);
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

