using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using SpendWise.Models;
using SpendWise.Services;
using System.Collections.Generic;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace SpendWise.Helpers
{
    public class ExternalServiceHelper
    {
        private readonly AzureOpenAIService _openAIService;
        private readonly IUserDataService _userService;
        private readonly IExpenseService _expenseService;
        private readonly IBudgetService _budgetService;
        private readonly IGoalService _goalService;

        public ExternalServiceHelper(
            AzureOpenAIService openAIService,
            IUserDataService userService,
            IExpenseService expenseService,
            IBudgetService budgetService,
            IGoalService goalService)
        {
            _openAIService = openAIService;
            _userService = userService;
            _expenseService = expenseService;
            _budgetService = budgetService;
            _goalService = goalService;
        }

        public async Task<(string response, string functionName, object parameters)> HandleUserQueryAsync(
            string query,
            List<ChatTransaction> history,
            ITurnContext context)
        {
            var userId = context.TurnState.Get<string>("UserId");
            var (data, functionName, directResponse) =
                await _openAIService.HandleOpenAIResponseAsync(query, history, userId);

            if (!string.IsNullOrEmpty(directResponse))
                return (directResponse, null, null);

            try
            {
                switch (functionName)
                {
                    case "create_user_profile":
                        return await HandleUserProfileCreation((Dictionary<string, object>)data, userId);

                    case "log_expense":
                        return await HandleExpenseLogging((Dictionary<string, object>)data, userId);

                    case "create_budget":
                        return await HandleBudgetCreation((Dictionary<string, object>)data, userId);

                    case "set_goal":
                        return await HandleGoalSetting((Dictionary<string, object>)data, userId);

                    case "add_recurring_expenses":
                        return await HandleRecurringExpense((Dictionary<string, object>)data, userId);

                    case "get_expense_summary":
                        return await HandleExpenseSummary((Dictionary<string, object>)data, userId);

                    default:
                        return ("Unknown operation requested.", functionName, null);
                }
            }
            catch (Exception ex)
            {
                return ($"Error processing request: {ex.Message}", "error", null);
            }
        }

        private async Task<(string, string, object)> HandleUserProfileCreation(Dictionary<string, object> data, string userId)
        {
            var user = await _userService.GetUserDetailsAsync(userId);

            if (data.TryGetValue("salary", out var salaryValue) && decimal.TryParse(salaryValue.ToString(), out var salary))
            {
                user.Salary = salary;
            }
            else
            {
                return ("Invalid salary value.", "error", null);
            }

            if (data.TryGetValue("location_type", out var locationTypeValue))
            {
                user.LocationType = locationTypeValue.ToString();
            }
            else
            {
                return ("Invalid location type value.", "error", null);
            }

            if (data.TryGetValue("currency", out var currencyValue))
            {
                user.Currency = currencyValue.ToString();
            }
            else
            {
                return ("Invalid currency value.", "error", null);
            }

            await _userService.UpdateUserAsync(user);
            return ("Profile updated successfully!", "profile_updated", user);
        }

        private async Task<(string, string, object)> HandleExpenseLogging(Dictionary<string, object> data, string userId)
        {
            if (!data.TryGetValue("amount", out var amountValue) || !decimal.TryParse(amountValue.ToString(), out var amount))
            {
                return ("Invalid amount value.", "error", null);
            }

            if (!data.TryGetValue("category_id", out var categoryIdValue) || !int.TryParse(categoryIdValue.ToString(), out var categoryId))
            {
                return ("Invalid category ID value.", "error", null);
            }

            if (!data.TryGetValue("expense_date", out var expenseDateValue) || !DateOnly.TryParse(expenseDateValue.ToString(), out var expenseDate))
            {
                return ("Invalid expense date value.", "error", null);
            }

            var expense = new Expense
            {
                UserId = userId,
                Amount = amount,
                CategoryId = categoryId,
                Description = data.TryGetValue("description", out var desc) ? desc.ToString() : null,
                ExpenseDate = expenseDate,
                CreatedAt = DateTime.UtcNow
            };

            await _expenseService.AddExpenseAsync(expense);

            //var budgetStatus = await _budgetService.CheckBudgetStatus(userId, expense.CategoryId);
            var message = $"Expense logged.";

            return (message, "expense_logged", expense);
        }

        private async Task<(string, string, object)> HandleBudgetCreation(Dictionary<string, object> data, string userId)
        {
            if (!data.TryGetValue("budgets", out var budgetsValue) || !(budgetsValue is JsonElement budgetsElement) || budgetsElement.ValueKind != JsonValueKind.Array)
            {
                return ("Invalid budgets data.", "error", null);
            }

            var budgets = new List<Budget>();

            foreach (var budgetElement in budgetsElement.EnumerateArray())
            {
                if (!budgetElement.TryGetProperty("category_id", out var categoryIdValue) || !int.TryParse(categoryIdValue.ToString(), out var categoryId))
                {
                    return ("Invalid category ID value.", "error", null);
                }

                if (!budgetElement.TryGetProperty("amount", out var amountValue) || !decimal.TryParse(amountValue.ToString(), out var amount))
                {
                    return ("Invalid amount value.", "error", null);
                }

                if (!budgetElement.TryGetProperty("start_date", out var startDateValue) || !DateOnly.TryParse(startDateValue.ToString(), out var startDate))
                {
                    return ("Invalid start date value.", "error", null);
                }

                if (!budgetElement.TryGetProperty("end_date", out var endDateValue) || !DateOnly.TryParse(endDateValue.ToString(), out var endDate))
                {
                    return ("Invalid end date value.", "error", null);
                }

                var budget = new Budget
                {
                    UserId = userId,
                    CategoryId = categoryId,
                    Amount = amount,
                    StartDate = startDate,
                    EndDate = endDate
                };

                budgets.Add(budget);
            }

            foreach (var budget in budgets)
            {
                await _budgetService.CreateOrUpdateBudgetAsync(budget);
            }

            return ("Budgets updated successfully!", "budgets_updated", budgets);
        }

        private async Task<(string, string, object)> HandleGoalSetting(Dictionary<string, object> data, string userId)
        {
            if (!data.TryGetValue("goal_name", out var goalNameValue) || string.IsNullOrEmpty(goalNameValue.ToString()))
            {
                return ("Invalid goal name value.", "error", null);
            }

            if (!data.TryGetValue("target_amount", out var targetAmountValue) || !decimal.TryParse(targetAmountValue.ToString(), out var targetAmount))
            {
                return ("Invalid target amount value.", "error", null);
            }

            if (!data.TryGetValue("target_date", out var targetDateValue) || !DateOnly.TryParse(targetDateValue.ToString(), out var targetDate))
            {
                return ("Invalid target date value.", "error", null);
            }

            var goal = new Goal
            {
                UserId = userId,
                Name = goalNameValue.ToString(),
                TargetAmount = targetAmount,
                StartDate = DateOnly.FromDateTime(DateTime.Now),
                EndDate = targetDate
            };

            await _goalService.AddGoalAsync(goal);
            return ("Goal set successfully!", "goal_set", goal);
        }

        private async Task<(string, string, object)> HandleRecurringExpense(Dictionary<string, object> data, string userId)
        {
            if (!data.TryGetValue("amount", out var amountValue) || !decimal.TryParse(amountValue.ToString(), out var amount))
            {
                return ("Invalid amount value.", "error", null);
            }

            if (!data.TryGetValue("category_id", out var categoryIdValue) || !int.TryParse(categoryIdValue.ToString(), out var categoryId))
            {
                return ("Invalid category ID value.", "error", null);
            }

            if (!data.TryGetValue("next_due_date", out var nextDueDateValue) || !DateOnly.TryParse(nextDueDateValue.ToString(), out var nextDueDate))
            {
                return ("Invalid next due date value.", "error", null);
            }

            if (!data.TryGetValue("frequency", out var frequencyValue) || string.IsNullOrEmpty(frequencyValue.ToString()))
            {
                return ("Invalid frequency value.", "error", null);
            }

            var recurringExpense = new RecurringExpense
            {
                UserId = userId,
                Amount = amount,
                CategoryId = categoryId,
                Description = data.TryGetValue("description", out var desc) ? desc.ToString() : null,
                NextDueDate = nextDueDate,
                Frequency = frequencyValue.ToString()
            };

            await _expenseService.AddRecurringExpenseAsync(recurringExpense);
            return ("Recurring expense added successfully!", "recurring_expense_added", recurringExpense);
        }

        private async Task<(string, string, object)> HandleExpenseSummary(Dictionary<string, object> data, string userId)
        {
            if (!data.TryGetValue("start_date", out var startDateValue) || !DateOnly.TryParse(startDateValue.ToString(), out var startDate))
            {
                return ("Invalid start date value.", "error", null);
            }

            if (!data.TryGetValue("end_date", out var endDateValue) || !DateOnly.TryParse(endDateValue.ToString(), out var endDate))
            {
                return ("Invalid end date value.", "error", null);
            }

            var categoryIds = data.TryGetValue("category_ids", out var catIds) ?
                JsonSerializer.Deserialize<List<int>>(catIds.ToString()) : new List<int>();
            var includeAll = data.TryGetValue("include_all_categories", out var includeAllValue) && bool.TryParse(includeAllValue.ToString(), out var includeAllBool) && includeAllBool;

            List<Expense> summary = new List<Expense>();

            if (categoryIds.Count > 0)
            {
                foreach (var categoryId in categoryIds)
                {
                    var categorySummary = await _expenseService.GetExpenseSummaryAsync(userId, startDate.ToDateTime(TimeOnly.MinValue), endDate.ToDateTime(TimeOnly.MinValue), categoryId , includeAll);
                    summary.AddRange(categorySummary);
                }
            }
            else
            {
                summary = await _expenseService.GetExpenseSummaryAsync(userId, startDate.ToDateTime(TimeOnly.MinValue), endDate.ToDateTime(TimeOnly.MinValue), 0, includeAll);
            }

            return ($"Expense summary generated.", "expense_summary", summary);
        }


public async Task<string> IndentHandlingAsync(string response)
        {
            string sysMessage = """
            Classify user messages into two categories:
            1.Ending the conversation: Messages that indicate the user is concluding the interaction(e.g., "thank you," "okay," "bye," "got it").
            2.Service - related or other queries: Messages where the user is asking for services, making inquiries, or seeking further assistance.

            Respond with the following JSON format:
            -If the message indicates the end of the conversation: { "response": "YES"}
            -If the message is a service - related query or anything else: { "response": "SERVICE"}
            """;
            return await _openAIService.HandleUserQuery(response, sysMessage);
        }
    }
}