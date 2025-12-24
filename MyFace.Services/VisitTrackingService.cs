using MyFace.Data;
using MyFace.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MyFace.Services;

public class VisitTrackingService
{
    private readonly ApplicationDbContext _context;

    public VisitTrackingService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task TrackVisitAsync(string path, string? userAgent = null)
    {
        var visit = new PageVisit
        {
            VisitedAt = DateTime.UtcNow,
            Path = path,
            UserAgent = userAgent
        };

        _context.PageVisits.Add(visit);
        await _context.SaveChangesAsync();
    }

    public async Task<SiteStatistics> GetStatisticsAsync()
    {
        var now = DateTime.UtcNow;
        var todayStart = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
        var weekStart = DateTime.SpecifyKind(now.Date.AddDays(-(int)now.DayOfWeek), DateTimeKind.Utc);
        var monthStart = DateTime.SpecifyKind(new DateTime(now.Year, now.Month, 1), DateTimeKind.Utc);
        var quarterStart = DateTime.SpecifyKind(new DateTime(now.Year, ((now.Month - 1) / 3) * 3 + 1, 1), DateTimeKind.Utc);
        var yearStart = DateTime.SpecifyKind(new DateTime(now.Year, 1, 1), DateTimeKind.Utc);
        var fourHoursAgo = now.AddHours(-4);
        var fifteenMinutesAgo = now.AddMinutes(-15); // Active users in last 15 minutes

        var stats = new SiteStatistics
        {
            // Current period counts
            TodayCount = await _context.PageVisits.CountAsync(v => v.VisitedAt >= todayStart),
            WeekCount = await _context.PageVisits.CountAsync(v => v.VisitedAt >= weekStart),
            MonthCount = await _context.PageVisits.CountAsync(v => v.VisitedAt >= monthStart),
            QuarterCount = await _context.PageVisits.CountAsync(v => v.VisitedAt >= quarterStart),
            YearCount = await _context.PageVisits.CountAsync(v => v.VisitedAt >= yearStart),
            FourHourCount = await _context.PageVisits.CountAsync(v => v.VisitedAt >= fourHoursAgo),
            
            // Live user count (active in last 15 minutes)
            LiveUserCount = await _context.Users.CountAsync(u => u.LastSeenAt >= fifteenMinutesAgo),
            
            // Total all-time
            TotalCount = await _context.PageVisits.CountAsync(),
            
            // Total users
            TotalUsers = await _context.Users.CountAsync()
        };

        // Calculate activity metrics
        await CalculateActivityMetricsAsync(stats, now);

        // Calculate peaks
        stats.PeakDay = await GetPeakDayCountAsync();
        stats.PeakWeek = await GetPeakWeekCountAsync();
        stats.PeakMonth = await GetPeakMonthCountAsync();
        stats.PeakQuarter = await GetPeakQuarterCountAsync();
        stats.PeakYear = await GetPeakYearCountAsync();
        stats.PeakFourHours = await GetPeakFourHourCountAsync();

        return stats;
    }

    private async Task CalculateActivityMetricsAsync(SiteStatistics stats, DateTime now)
    {
        try
        {
            var minuteAgo = now.AddMinutes(-1);
            var hourAgo = now.AddHours(-1);
            var dayAgo = now.AddDays(-1);
            var weekAgo = now.AddDays(-7);
            var monthAgo = now.AddMonths(-1);
            var yearAgo = now.AddYears(-1);

            // Count activities in each time period
            var lastMinute = await _context.Activities.CountAsync(a => a.CreatedAt >= minuteAgo);
            var lastHour = await _context.Activities.CountAsync(a => a.CreatedAt >= hourAgo);
            var lastDay = await _context.Activities.CountAsync(a => a.CreatedAt >= dayAgo);
            var lastWeek = await _context.Activities.CountAsync(a => a.CreatedAt >= weekAgo);
            var lastMonth = await _context.Activities.CountAsync(a => a.CreatedAt >= monthAgo);
            var lastYear = await _context.Activities.CountAsync(a => a.CreatedAt >= yearAgo);

            // Calculate rates
            stats.ActionsPerMinute = lastMinute;
            stats.ActionsPerHour = lastHour;
            stats.ActionsPerDay = lastDay;
            stats.ActionsPerWeek = lastWeek;
            stats.ActionsPerMonth = lastMonth;
            stats.ActionsPerYear = lastYear;
        }
        catch
        {
            // Silently fail if Activities table doesn't exist yet or has issues
            stats.ActionsPerMinute = 0;
            stats.ActionsPerHour = 0;
            stats.ActionsPerDay = 0;
            stats.ActionsPerWeek = 0;
            stats.ActionsPerMonth = 0;
            stats.ActionsPerYear = 0;
        }
    }

    private async Task<int> GetPeakDayCountAsync()
    {
        var result = await _context.PageVisits
            .GroupBy(v => v.VisitedAt.Date)
            .Select(g => g.Count())
            .OrderByDescending(c => c)
            .FirstOrDefaultAsync();
        return result;
    }

    private async Task<int> GetPeakWeekCountAsync()
    {
        var visits = await _context.PageVisits.ToListAsync();
        if (!visits.Any()) return 0;
        
        var weekGroups = visits
            .GroupBy(v => GetWeekStart(v.VisitedAt))
            .Select(g => g.Count())
            .OrderByDescending(c => c);
        
        return weekGroups.FirstOrDefault();
    }

    private async Task<int> GetPeakMonthCountAsync()
    {
        var result = await _context.PageVisits
            .GroupBy(v => new { v.VisitedAt.Year, v.VisitedAt.Month })
            .Select(g => g.Count())
            .OrderByDescending(c => c)
            .FirstOrDefaultAsync();
        return result;
    }

    private async Task<int> GetPeakQuarterCountAsync()
    {
        var visits = await _context.PageVisits.ToListAsync();
        if (!visits.Any()) return 0;
        
        var quarterGroups = visits
            .GroupBy(v => new { v.VisitedAt.Year, Quarter = ((v.VisitedAt.Month - 1) / 3) + 1 })
            .Select(g => g.Count())
            .OrderByDescending(c => c);
        
        return quarterGroups.FirstOrDefault();
    }

    private async Task<int> GetPeakYearCountAsync()
    {
        var result = await _context.PageVisits
            .GroupBy(v => v.VisitedAt.Year)
            .Select(g => g.Count())
            .OrderByDescending(c => c)
            .FirstOrDefaultAsync();
        return result;
    }

    private async Task<int> GetPeakFourHourCountAsync()
    {
        var visits = await _context.PageVisits
            .Where(v => v.VisitedAt >= DateTime.UtcNow.AddDays(-7)) // Last week for performance
            .ToListAsync();
        
        if (!visits.Any()) return 0;

        var maxCount = 0;
        foreach (var visit in visits)
        {
            var fourHoursAfter = visit.VisitedAt.AddHours(4);
            var count = visits.Count(v => v.VisitedAt >= visit.VisitedAt && v.VisitedAt < fourHoursAfter);
            if (count > maxCount) maxCount = count;
        }
        
        return maxCount;
    }

    private DateTime GetWeekStart(DateTime date)
    {
        return DateTime.SpecifyKind(date.Date.AddDays(-(int)date.DayOfWeek), DateTimeKind.Utc);
    }
}

public class SiteStatistics
{
    public int TodayCount { get; set; }
    public int WeekCount { get; set; }
    public int MonthCount { get; set; }
    public int QuarterCount { get; set; }
    public int YearCount { get; set; }
    public int FourHourCount { get; set; }
    public int LiveUserCount { get; set; }
    public int TotalCount { get; set; }
    
    public int PeakDay { get; set; }
    public int PeakWeek { get; set; }
    public int PeakMonth { get; set; }
    public int PeakQuarter { get; set; }
    public int PeakYear { get; set; }
    public int PeakFourHours { get; set; }
    
    // User and activity metrics
    public int TotalUsers { get; set; }
    public double ActionsPerMinute { get; set; }
    public double ActionsPerHour { get; set; }
    public double ActionsPerDay { get; set; }
    public double ActionsPerWeek { get; set; }
    public double ActionsPerMonth { get; set; }
    public double ActionsPerYear { get; set; }
}
