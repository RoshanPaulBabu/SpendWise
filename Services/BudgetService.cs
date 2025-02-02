using SpendWise.Models;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SpendWise.Data;
using System.Linq;
using System;
using Microsoft.Extensions.Logging;

namespace SpendWise.Services
{
    public interface IBudgetService
    {
        Task<bool> CheckBudgetStatus(string userId, int categoryId);
        Task CreateOrUpdateBudgetAsync(Budget budget);

        Task<string> GetBudgetsAsStringAsync(string userId);
    }

    public class BudgetService : IBudgetService
    {
        private readonly SpendWiseContext _context;
        private readonly ILogger<BudgetService> _logger;

        public BudgetService(SpendWiseContext context, ILogger<BudgetService> logger)
        {
            _context = context;
            _logger = logger;
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
        public async Task<string> GetBudgetsAsStringAsync(string userId)
        {
            try
            {
                // Ensure userId is not null or empty
                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));
                }

                // Fetch active budgets from the database
                var budgets = await _context.Budgets
                    .Where(b => b.UserId == userId && b.EndDate >= DateOnly.FromDateTime(DateTime.Now))
                    .Include(b => b.Category)
                    .ToListAsync();

                if (budgets == null || budgets.Count == 0)
                {
                    return "No active budgets set.";
                }

                // Build the budgets string
                var budgetStrings = budgets.Select(b =>
                    $"{b.Category.Name}: {b.Amount.ToString("C")} from {b.StartDate:yyyy-MM-dd} to {b.EndDate:yyyy-MM-dd}"
                );

                return string.Join(", ", budgetStrings);
            }
            catch (Exception ex)
            {
                // Log the exception details
                _logger.LogError(ex, $"Error in GetBudgetsAsStringAsync for User ID: {userId}");

                // Optionally, rethrow the exception or return a default value
                // throw;
                return null;
            }
        }

    }
}
