using SpendWise.Data;
using SpendWise.Models;
using SpendWise.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace SpendWise.Services
{
    public interface IExpenseService
    {
        Task AddExpenseAsync(Expense expense);
        Task AddRecurringExpenseAsync(RecurringExpense recurringExpense);
        Task<List<Expense>> GetExpenseSummaryAsync(string userId, DateTime startDate, DateTime endDate, int categoryId, bool includeAll);
        Task<Dictionary<string, decimal>> GetTotalAmountByCategoryForCurrentMonthAsync(string userId);
        Task<string> GetRecurringExpensesAsStringAsync(string userId);
    }
    public class ExpenseService : IExpenseService
    {
        private readonly SpendWiseContext _context;

        public ExpenseService(SpendWiseContext context)
        {
            _context = context;
        }

        public async Task AddExpenseAsync(Expense expense)
        {
            await _context.Expenses.AddAsync(expense);
            await _context.SaveChangesAsync();
        }

        public async Task AddRecurringExpenseAsync(RecurringExpense recurringExpense)
        {
            await _context.RecurringExpenses.AddAsync(recurringExpense);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Expense>> GetExpenseSummaryAsync(string userId, DateTime startDate, DateTime endDate, int categoryId, bool includeAll)
        {
            var query = _context.Expenses
                .Include(e => e.Category)
                .Where(e => e.UserId == userId && e.ExpenseDate.ToDateTime(TimeOnly.MinValue) >= startDate && e.ExpenseDate.ToDateTime(TimeOnly.MinValue) <= endDate);

            if (!includeAll)
            {
                query = query.Where(e => e.Category.CategoryId == categoryId);
            }

            return await query.ToListAsync();
        }
        public async Task<Dictionary<string, decimal>> GetTotalAmountByCategoryForCurrentMonthAsync(string userId)
        {
            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;

            var result = await _context.Expenses
                .Where(e => e.UserId == userId && e.ExpenseDate.Month == currentMonth && e.ExpenseDate.Year == currentYear)
                .GroupBy(e => e.Category.Name)
                .Select(g => new { Category = g.Key, TotalAmount = g.Sum(e => e.Amount) })
                .ToDictionaryAsync(g => g.Category, g => g.TotalAmount);

            return result;
        }
        public async Task<string> GetRecurringExpensesAsStringAsync(string userId)
        {
            var recurringExpenses = await _context.RecurringExpenses
                .Where(re => re.UserId == userId)
                .ToListAsync();

            var result = string.Join(", ", recurringExpenses.Select(re => $"{re.Description} {re.Amount} {re.Frequency} {re.NextDueDate}"));

            return result;
        }


       
    }

}