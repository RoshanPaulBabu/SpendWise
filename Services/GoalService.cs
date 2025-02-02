using SpendWise.Models;
using System.Threading.Tasks;
using SpendWise.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System;

namespace SpendWise.Services
{
    public interface IGoalService
    {
        Task AddGoalAsync(Goal goal);
        Task<string> GetActiveGoalsAsStringAsync(string userId);
    }

    public class GoalService : IGoalService
    {
        private readonly SpendWiseContext _context;

        public GoalService(SpendWiseContext context)
        {
            _context = context;
        }

        public async Task AddGoalAsync(Goal goal)
        {
            await _context.Goals.AddAsync(goal);
            await _context.SaveChangesAsync();
        }

        public async Task<string> GetActiveGoalsAsStringAsync(string userId)
        {
            var activeGoals = await _context.Goals
                .Where(g => g.UserId == userId && (g.EndDate == null || g.EndDate >= DateOnly.FromDateTime(DateTime.Now)))
                .ToListAsync();

            return string.Join(", ", activeGoals.Select(g => $"{g.Name} (Target: {g.TargetAmount}, End Date: {g.EndDate?.ToString("yyyy-MM-dd") ?? "No End Date"})"));
        }
    }
}
