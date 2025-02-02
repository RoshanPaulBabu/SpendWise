using SpendWise.Models;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SpendWise.Data;
using System.Linq;

namespace SpendWise.Services
{
    public interface IBudgetService
    {
        Task<bool> CheckBudgetStatus(string userId, int categoryId);
        Task CreateOrUpdateBudgetAsync(Budget budget);
    }

    public class BudgetService : IBudgetService
    {
        private readonly SpendWiseContext _context;

        public BudgetService(SpendWiseContext context)
        {
            _context = context;
        }

        public async Task<bool> CheckBudgetStatus(string userId, int categoryId)
        {
            var budget = await _context.Budgets
                .FirstOrDefaultAsync(b => b.UserId == userId && b.CategoryId == categoryId);

            if (budget == null)
                return false;

            var totalExpenses = await _context.Expenses
                .Where(e => e.UserId == userId && e.CategoryId == categoryId)
                .SumAsync(e => e.Amount);

            return totalExpenses > budget.Amount;
        }

        public async Task CreateOrUpdateBudgetAsync(Budget budget)
        {
            var existingBudget = await _context.Budgets
                .FirstOrDefaultAsync(b => b.UserId == budget.UserId && b.CategoryId == budget.CategoryId);

            if (existingBudget != null)
            {
                existingBudget.Amount = budget.Amount;
                _context.Budgets.Update(existingBudget);
            }
            else
            {
                await _context.Budgets.AddAsync(budget);
            }

            await _context.SaveChangesAsync();
        }
    }
}
