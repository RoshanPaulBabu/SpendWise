using SpendWise.Models;
using System.Threading.Tasks;
using SpendWise.Data;

namespace SpendWise.Services
{
    public interface IGoalService
    {
        Task AddGoalAsync(Goal goal);
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
    }
}
