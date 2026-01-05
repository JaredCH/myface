# Thread Display System - Hot Algorithm Improvements (R0)

**Repository:** JaredCH/myface  
**Sprint Start Date:** 2026-01-05  
**Status:** Planning Phase  
**Current User:** JaredCH

---

## Overview

Improve the existing Wilson Score + Time Decay algorithm to prioritize currently active threads over historically popular but inactive threads.

---

## Problem Statement

Current hot algorithm uses thread creation date for time decay, causing:
- Old threads with many comments stay "hot" even with no recent activity
- New threads with active discussion rank lower than old inactive threads
- Users want to see which threads are currently active, not just historically popular
- Example: Thread with 100 comments (last 7 days ago) ranks higher than thread with 5 comments (all in last hour)

---

## Current Implementation

**File:** `MyFace.Services/ForumService.cs` (lines 669-740)

**Algorithm:**
- Wilson Score Confidence Interval (95% confidence)
- Time decay: `1.0 / (daysOld + 0.5)`
- Thread votes: 70% weight, post votes: 30% weight
- Result scaled by 1000

**Issues:**
1. Uses `thread.CreatedAt` for decay (should use last activity time)
2. Uses days instead of hours (decay too slow)
3. Linear decay (should be exponential for faster drop-off)
4. No consideration for recent comment velocity

---

## Solution

Keep existing Wilson Score (proven, solid) but improve time decay to favor recent activity.

### Key Changes

1. Add `LastActivityAt` field to Thread entity
2. Update `LastActivityAt` on every new post
3. Change decay from days to hours
4. Add exponential decay (gravity)
5. Add recent activity boost (optional)

---

## Phase 1: Database Schema

### Step 1.1: Add LastActivityAt Column ⬜
- [ ] Add `LastActivityAt` column to Threads table (DateTime)
- [ ] Create migration
- [ ] Set initial value: `COALESCE(UpdatedAt, CreatedAt)`
- [ ] Add NOT NULL constraint
- [ ] Add index on `LastActivityAt` for query performance

**Migration SQL:**
```sql
ALTER TABLE "Threads" ADD COLUMN "LastActivityAt" timestamp with time zone;
UPDATE "Threads" SET "LastActivityAt" = COALESCE("UpdatedAt", "CreatedAt");
ALTER TABLE "Threads" ALTER COLUMN "LastActivityAt" SET NOT NULL;
CREATE INDEX "IX_Threads_LastActivityAt" ON "Threads" ("LastActivityAt");
Step 1.2: Update Thread Entity ⬜
 Add LastActivityAt property to Thread.cs entity
 Verify migration applies cleanly
 Test database updates
File: MyFace.Core/Entities/Thread.cs

Phase 2: Update Post Creation Logic
Step 2.1: Update CreatePostAsync ⬜
 Modify ForumService.CreatePostAsync method
 After creating post, update thread's LastActivityAt
 Set to DateTime.UtcNow
 Ensure it saves with the post transaction
File: MyFace.Services/ForumService.cs (around line 108)

Changes:

C#
public async Task<Post> CreatePostAsync(int threadId, string content, int? userId, bool isAnonymous)
{
    // ... existing code ...
    
    _context.Posts.Add(post);
    TrackActivity("comment", userId);
    
    // NEW: Update thread's last activity time
    var thread = await _context.Threads.FindAsync(threadId);
    if (thread != null)
    {
        thread.LastActivityAt = DateTime.UtcNow;
    }
    
    await _context.SaveChangesAsync();
    return post;
}
Step 2.2: Update CreateThreadAsync ⬜
 Set LastActivityAt = CreatedAt when creating new thread
 Ensures new threads have initial activity timestamp
File: MyFace.Services/ForumService.cs (around line 33)

Phase 3: Improve Hot Score Algorithm
Step 3.1: Update CalculateHotScore Method ⬜
 Change signature to accept DateTime lastActivityAt instead of createdAt
 Change from days to hours
 Add exponential decay (gravity exponent)
 Keep Wilson score calculation (don't change)
File: MyFace.Services/ForumService.cs (line 671)

Current:

C#
public double CalculateHotScore(int upvotes, int downvotes, DateTime createdAt)
{
    int total = upvotes + downvotes;
    if (total == 0) return 0;

    // Wilson score confidence interval
    double z = 1.96;
    double phat = (double)upvotes / total;
    double wilson = (phat + z * z / (2 * total) - z * Math.Sqrt((phat * (1 - phat) + z * z / (4 * total)) / total)) / (1 + z * z / total);

    // Time decay
    double daysOld = (DateTime.UtcNow - createdAt).TotalDays;
    double timeDecay = 1.0 / (daysOld + 0.5);

    return wilson * timeDecay * 1000;
}
New:

C#
public double CalculateHotScore(int upvotes, int downvotes, DateTime lastActivityAt, double gravity = 1.5)
{
    int total = upvotes + downvotes;
    if (total == 0) return 0;

    // Wilson score confidence interval (unchanged - this is good)
    double z = 1.96;
    double phat = (double)upvotes / total;
    double wilson = (phat + z * z / (2 * total) - z * Math.Sqrt((phat * (1 - phat) + z * z / (4 * total)) / total)) / (1 + z * z / total);

    // NEW: Time decay based on hours since last activity
    double hoursOld = (DateTime.UtcNow - lastActivityAt).TotalHours;
    double timeDecay = 1.0 / Math.Pow(hoursOld + 2, gravity);

    return wilson * timeDecay * 1000;
}
Changes:

Parameter: createdAt → lastActivityAt
Units: .TotalDays → .TotalHours
Decay: (daysOld + 0.5) → Math.Pow(hoursOld + 2, 1.5)
Impact:

1 hour old: decay = 0.136
6 hours old: decay = 0.044
24 hours old: decay = 0.015
72 hours old: decay = 0.0049
168 hours (7 days) old: decay = 0.00044
Much steeper decay curve = more responsive to recent activity.

Step 3.2: Update CalculateHotScoreForThread ⬜
 Pass thread.LastActivityAt instead of thread.CreatedAt
 Ensure method signature matches
File: MyFace.Services/ForumService.cs (line 724)

Change:

C#
private double CalculateHotScoreForThread(ThreadEntity thread)
{
    // ... existing Wilson score calculation ...
    
    // CHANGED: Use LastActivityAt instead of CreatedAt
    return CalculateHotScore(totalUpvotes, totalDownvotes, thread.LastActivityAt);
}
Step 3.3: Update GetHotPostsForThreadAsync ⬜
 Update post hot score calculation to use post.CreatedAt (posts don't have LastActivityAt)
 Posts are single items, so creation time is appropriate
File: MyFace.Services/ForumService.cs (line 742)

No change needed - posts use p.CreatedAt which is correct for individual posts.

Phase 4: Optional Enhancement - Recent Activity Boost
Step 4.1: Add Recent Activity Count ⬜
 Count posts in last 72 hours
 Add logarithmic activity boost to hot score
 Prevents threads with 100 recent comments from overwhelming threads with 5
Implementation:

C#
private double CalculateHotScoreForThread(ThreadEntity thread)
{
    // Existing vote calculations
    var threadVotes = thread.Votes.ToList();
    int threadUpvotes = threadVotes.Count(v => v.Value > 0);
    int threadDownvotes = threadVotes.Count(v => v.Value < 0);
    
    var postVotes = thread.Posts.SelectMany(p => p.Votes).ToList();
    int postUpvotes = postVotes.Count(v => v.Value > 0);
    int postDownvotes = postVotes.Count(v => v.Value < 0);
    
    int totalUpvotes = (int)(threadUpvotes * 0.7 + postUpvotes * 0.3);
    int totalDownvotes = (int)(threadDownvotes * 0.7 + postDownvotes * 0.3);
    
    // NEW: Count recent posts (last 72 hours)
    var recentCutoff = DateTime.UtcNow.AddHours(-72);
    int recentPostCount = thread.Posts.Count(p => p.CreatedAt >= recentCutoff && !p.IsDeleted);
    
    // NEW: Activity boost (logarithmic)
    double activityBoost = 1.0 + Math.Log(recentPostCount + 1);
    
    // Wilson score with LastActivityAt
    double baseScore = CalculateHotScore(totalUpvotes, totalDownvotes, thread.LastActivityAt);
    
    // Apply activity boost
    return baseScore * activityBoost;
}
Activity Boost Examples:

0 recent posts: 1.0 + log(1) = 1.0x (no boost)
1 recent post: 1.0 + log(2) = 1.69x
5 recent posts: 1.0 + log(6) = 2.79x
10 recent posts: 1.0 + log(11) = 3.46x
20 recent posts: 1.0 + log(21) = 4.04x
Logarithmic scaling prevents over-amplification.

Step 4.2: Make Time Window Configurable ⬜
 Add HotThreads:RecentActivityHours to ControlSettings
 Default: 72 hours
 Allow admins to adjust as site grows
 Use _settings.GetIntAsync() to retrieve
Configuration:

C#
var recentHours = await _settings.GetIntAsync("HotThreads:RecentActivityHours", 72);
var recentCutoff = DateTime.UtcNow.AddHours(-recentHours);
Future adjustments:

Small community (now): 72 hours
Growing (100+ daily users): 48 hours
Medium (1000+ daily users): 24 hours
Large (10,000+ daily users): 12 hours
Phase 5: Make Gravity Configurable
Step 5.1: Add Gravity Setting ⬜
 Add HotThreads:DecayGravity to ControlSettings
 Default: 1.5
 Higher gravity = faster decay
 Allow tuning without code changes
Implementation:

C#
public async Task<double> CalculateHotScoreAsync(int upvotes, int downvotes, DateTime lastActivityAt)
{
    var gravity = await _settings.GetDoubleAsync("HotThreads:DecayGravity", 1.5);
    
    // ... Wilson score ...
    
    double hoursOld = (DateTime.UtcNow - lastActivityAt).TotalHours;
    double timeDecay = 1.0 / Math.Pow(hoursOld + 2, gravity);
    
    return wilson * timeDecay * 1000;
}
Gravity values:

1.0 = slow decay (very forgiving)
1.5 = moderate decay (recommended start)
1.8 = fast decay (for active communities)
2.0 = very fast decay (Hacker News style)
Phase 6: Testing
Step 6.1: Unit Tests ⬜
 Test CalculateHotScore with various inputs
 Test time decay at 1 hour, 24 hours, 72 hours, 7 days
 Test Wilson score with different vote ratios
 Test activity boost with 0, 1, 5, 10, 20 recent posts
 Verify gravity parameter works
Test cases:

Code
Thread A: 100 upvotes, 10 downvotes, last activity 168 hours ago
Thread B: 10 upvotes, 1 downvote, last activity 1 hour ago
Thread C: 5 upvotes, 0 downvotes, last activity 2 hours ago, 5 recent posts

Expected: B or C should rank higher than A
Step 6.2: Integration Tests ⬜
 Test GetHotThreadsAsync returns threads in correct order
 Test creating new post updates thread's LastActivityAt
 Test threads with recent activity appear higher
 Test old threads fade from hot list
Step 6.3: Manual Testing ⬜
 View /thread page (Hot tab)
 Verify active threads appear first
 Post comment on old thread
 Verify that thread moves up in hot list
 Wait several hours, verify thread decays appropriately
Step 6.4: Performance Testing ⬜
 Test query performance with 1000+ threads
 Verify index on LastActivityAt is used
 Check query execution time (should be <100ms)
 Monitor database load
Phase 7: Monitoring & Tuning
Step 7.1: Add Hot Score Logging (Optional) ⬜
 Log hot scores for debugging
 Add admin page to view hot scores
 Display: thread title, score, hours since activity, recent posts
Debug view:

Code
Thread: "Example Title"
Hot Score: 523.4
Hours since activity: 12.5
Recent posts (72h): 8
Wilson score: 0.85
Time decay: 0.021
Activity boost: 3.17
Step 7.2: Monitor Hot Tab Usage ⬜
 Track page views of Hot tab vs New/News/Announcements
 Monitor click-through rate on hot threads
 Gather user feedback
 Identify if hot threads are truly active
Step 7.3: Tune Parameters ⬜
 Adjust gravity based on community activity
 Adjust recent activity time window
 Test different activity boost formulas if needed
 Document changes and results
Phase 8: Deployment
Step 8.1: Pre-Deployment Checklist ⬜
 All tests passing
 Database migration tested on staging
 Backup production database
 Code reviewed
 Documentation updated
Step 8.2: Staging Deployment ⬜
 Deploy to staging environment
 Run migration
 Test hot algorithm with staging data
 Verify LastActivityAt updates correctly
 Check performance
Step 8.3: Production Deployment ⬜
 Deploy to production
 Run migration
 Monitor error logs
 Check hot tab immediately
 Verify threads rank correctly
Step 8.4: Post-Deployment ⬜
 Monitor for 24 hours
 Check hot thread rankings
 Verify no performance degradation
 Gather user feedback
 Adjust settings if needed
Expected Results
Before Implementation
Hot threads ranked by total historical activity:

Thread A: 100 comments, last comment 7 days ago
Thread B: 50 comments, last comment 5 days ago
Thread C: 5 comments, last comment 1 hour ago
After Implementation
Hot threads ranked by recent activity:

Thread C: 5 comments, last comment 1 hour ago ⭐
Thread D: 3 comments, last comment 2 hours ago, 3 recent posts
Thread A: 100 comments, last comment 7 days ago (faded)
Configuration Reference
Recommended Settings (Small Community)
Code
HotThreads:RecentActivityHours = 72
HotThreads:DecayGravity = 1.5
As Community Grows
100 daily active users:

Code
HotThreads:RecentActivityHours = 48
HotThreads:DecayGravity = 1.6
1,000 daily active users:

Code
HotThreads:RecentActivityHours = 24
HotThreads:DecayGravity = 1.8
10,000+ daily active users:

Code
HotThreads:RecentActivityHours = 12
HotThreads:DecayGravity = 2.0
Rollback Plan
If Issues Occur
Revert code changes:

Change CalculateHotScore back to use createdAt
Change back to days instead of hours
Remove activity boost
Redeploy
Database migration is safe:

LastActivityAt column can remain
Doesn't break existing functionality
Can be used in future improvements
Settings can be adjusted:

Increase gravity to slow decay
Increase time window to 168 hours (7 days)
Disable activity boost by setting window to 0
Future Enhancements
Phase 2 Improvements (Future)
 Separate hot algorithms per category (Hot, News, Announcements)
 Add "Rising" tab (threads gaining velocity)
 Add "Controversial" tab (high engagement, mixed votes)
 Weight first 10 comments more than later comments
 Penalize threads with downvote ratio above threshold
 Boost threads from verified users (slight)
 Add "Trending" calculation (velocity-based)
Advanced Features
 Machine learning to predict which threads will be popular
 Personalized hot feeds based on user interests
 A/B testing different hot algorithms
 Time-of-day adjustments (weekend vs weekday)
 Category-specific decay rates
Notes & Discoveries
[2026-01-05] - Initial Document Creation
Current algorithm uses Wilson Score + Time Decay (solid foundation)
Main issue: uses CreatedAt instead of LastActivityAt
Decay is too slow (days instead of hours)
Solution: Keep Wilson Score, improve time decay, add activity boost
Agent Instructions
TO FUTURE AI AGENTS WORKING ON THIS SPRINT:

Read this document first
Check off completed items [x]
Add notes below with date, files modified, test results
Update configuration values based on testing
Document any deviations from plan
If algorithm doesn't work as expected, log hot scores for debugging
Notes Section (Add entries below)
Summary Progress
 Phase 1: Database Schema (0/2)
 Phase 2: Post Creation Logic (0/2)
 Phase 3: Hot Score Algorithm (0/3)
 Phase 4: Activity Boost (0/2) - OPTIONAL
 Phase 5: Configurable Gravity (0/1) - OPTIONAL
 Phase 6: Testing (0/4)
 Phase 7: Monitoring (0/3) - OPTIONAL
 Phase 8: Deployment (0/4)
Core Implementation: 0/11 steps Total with Optional: 0/21 steps

Quick Reference
Key Formula:

Code
hotScore = wilsonScore * timeDecay * activityBoost * 1000

where:
- wilsonScore = Wilson confidence interval (existing)
- timeDecay = 1.0 / (hoursOld + 2)^gravity
- activityBoost = 1.0 + log(recentPosts + 1)
- gravity = 1.5 (configurable)
- recentPosts = posts in last 72 hours (configurable)
Files to modify:

MyFace.Core/Entities/Thread.cs - Add LastActivityAt
MyFace.Services/ForumService.cs - Update methods (3 changes)
MyFace.Data/Migrations/ - Add migration
Database changes:

Add LastActivityAt column to Threads table
Add index on LastActivityAt
