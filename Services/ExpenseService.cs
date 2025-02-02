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
        Task<List<Expense>> GetExpenseSummaryAsync(string userId, DateTime startDate, DateTime endDate, List<int> categoryIds, bool includeAll);
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

        public async Task<List<Expense>> GetExpenseSummaryAsync(string userId, DateTime startDate, DateTime endDate, List<int> categoryIds, bool includeAll)
        {
            var query = _context.Expenses.Where(e => e.UserId == userId && e.ExpenseDate.ToDateTime(TimeOnly.MinValue) >= startDate && e.ExpenseDate.ToDateTime(TimeOnly.MinValue) <= endDate);

            if (!includeAll)
            {
                query = query.Where(e => categoryIds.Contains(e.CategoryId));
            }

            return await query.ToListAsync();
        }
    }

}