using Microsoft.EntityFrameworkCore;
using MyFace.Data;

namespace MyFace.Services;

public class ReputationService
{
    private readonly ApplicationDbContext _context;

    public ReputationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> GetUserReputationAsync(int userId)
    {
        var votes = await _context.Votes
            .Where(v => v.UserId == userId || (v.Post.UserId == userId))
            .ToListAsync();
        // Reputation: sum of vote values on user's posts; ignore votes cast
        return votes.Where(v => v.Post.UserId == userId).Sum(v => v.Value);
    }
}
